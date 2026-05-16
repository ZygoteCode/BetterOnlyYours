import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

class SecurityCore {
  static final Uint8List magicBytes = Uint8List.fromList([
    0xFF,
    0xFE,
    0x0D,
    0x0A,
  ]);

  static const int _version = 1;
  static const int _saltLength = 16;
  static const int _nonceLength = 12; // GCM standard nonce size
  static const int _keyLength = 32; // 256-bit key
  static const int _pbkdf2Iterations = 60000;

  static final Random _secureRandom = Random.secure();

  static Uint8List? _cachedKey;
  static Uint8List? _cachedSalt;
  static String? _cachedPassword;

  static Future<bool> vaultExists() async {
    final file = File('credentials.plf');
    return file.exists();
  }

  static Uint8List _randomBytes(int length) {
    final bytes = Uint8List(length);
    for (var i = 0; i < length; i++) {
      bytes[i] = _secureRandom.nextInt(256);
    }
    return bytes;
  }

  static Uint8List getKeccakHash(Uint8List input, {int bits = 512}) {
    final digest = KeccakDigest(bits);
    final result = Uint8List(digest.digestSize);
    digest.update(input, 0, input.length);
    digest.doFinal(result, 0);
    return result;
  }

  static void secureWipe(Uint8List data) {
    data.fillRange(0, data.length, 0);
  }

  static Uint8List combine(List<Uint8List> arrays) {
    final totalLength = arrays.fold<int>(0, (sum, element) => sum + element.length);
    final result = Uint8List(totalLength);
    var offset = 0;
    for (final array in arrays) {
      result.setAll(offset, array);
      offset += array.length;
    }
    return result;
  }

  static bool compareByteArrays(Uint8List a, Uint8List b) {
    if (a.length != b.length) return false;

    var diff = 0;
    for (var i = 0; i < a.length; i++) {
      diff |= a[i] ^ b[i];
    }
    return diff == 0;
  }

  static Future<void> atomicWrite(String path, Uint8List data) async {
    final targetFile = File(path);
    final tempFile = File('$path.tmp');
    final backupFile = File('$path.bak');

    try {
      final raf = await tempFile.open(mode: FileMode.write);
      await raf.writeFrom(data);
      await raf.flush();
      await raf.close();

      if (await targetFile.exists()) {
        if (await backupFile.exists()) {
          await backupFile.delete();
        }
        await targetFile.copy(backupFile.path);
        await targetFile.delete();
      }

      await tempFile.copy(path);
      await tempFile.delete();
    } catch (e) {
      if (await tempFile.exists()) {
        await tempFile.delete();
      }
      throw Exception("Critical Error during IO atomic write: $e");
    }
  }

  static Uint8List _deriveKey(Uint8List passwordBytes, Uint8List salt) {
    final password = utf8.decode(passwordBytes);

    if (_cachedKey != null &&
        _cachedSalt != null &&
        _cachedPassword == password &&
        compareByteArrays(_cachedSalt!, salt)) {
      return Uint8List.fromList(_cachedKey!);
    }

    final derivator = PBKDF2KeyDerivator(
      HMac(SHA256Digest(), 64),
    )..init(
      Pbkdf2Parameters(
        salt,
        _pbkdf2Iterations,
        _keyLength,
      ),
    );

    final key = derivator.process(passwordBytes);

    _cachedKey = Uint8List.fromList(key);
    _cachedSalt = Uint8List.fromList(salt);
    _cachedPassword = password;

    return key;
  }

  static Uint8List _gcmCrypt({
    required bool forEncryption,
    required Uint8List key,
    required Uint8List nonce,
    required Uint8List input,
    required Uint8List aad,
  }) {
    final cipher = GCMBlockCipher(AESEngine());
    cipher.init(
      forEncryption,
      AEADParameters(
        KeyParameter(key),
        128, // auth tag length in bits
        nonce,
        aad,
      ),
    );
    return cipher.process(input);
  }

  static Future<void> saveAllCredentials(
      Map<String, String> credentials,
      String password,
      ) async {
    final passBytes = Uint8List.fromList(utf8.encode(password));
    final salt = _randomBytes(_saltLength);
    final nonce = _randomBytes(_nonceLength);
    final keyBytes = _deriveKey(passBytes, salt);

    final jsonString = const JsonEncoder.withIndent('  ').convert(credentials);
    final plaintext = Uint8List.fromList(utf8.encode(jsonString));

    final header = combine([
      magicBytes,
      Uint8List.fromList([_version]),
      salt,
      nonce,
    ]);

    try {
      final encrypted = _gcmCrypt(
        forEncryption: true,
        key: keyBytes,
        nonce: nonce,
        input: plaintext,
        aad: header,
      );

      final finalFileBytes = combine([header, encrypted]);
      await atomicWrite('credentials.plf', finalFileBytes);
    } finally {
      secureWipe(passBytes);
      secureWipe(keyBytes);
      secureWipe(plaintext);
    }
  }

  static Future<Map<String, String>> loadAllCredentials(String password) async {
    final file = File('credentials.plf');
    if (!await file.exists()) {
      throw Exception("Vault file not found.");
    }

    final rawFile = await file.readAsBytes();
    const headerLength = 4 + 1 + _saltLength + _nonceLength;

    if (rawFile.length < headerLength) {
      throw Exception("File structure layout too short.");
    }

    final fileMagic = rawFile.sublist(0, 4);
    if (!compareByteArrays(fileMagic, magicBytes)) {
      throw Exception("Corrupted file signature (Magic bytes mismatch).");
    }

    final version = rawFile[4];
    if (version != _version) {
      throw Exception("Unsupported vault version.");
    }

    final salt = rawFile.sublist(5, 5 + _saltLength);
    final nonce = rawFile.sublist(5 + _saltLength, headerLength);
    final encrypted = rawFile.sublist(headerLength);

    if (encrypted.isEmpty) {
      throw Exception("Encrypted payload is missing.");
    }

    final header = rawFile.sublist(0, headerLength);

    final passBytes = Uint8List.fromList(utf8.encode(password));
    final keyBytes = _deriveKey(passBytes, salt);

    try {
      final decrypted = _gcmCrypt(
        forEncryption: false,
        key: keyBytes,
        nonce: nonce,
        input: encrypted,
        aad: header,
      );

      final jsonString = utf8.decode(decrypted);
      secureWipe(decrypted);

      final decodedMap = jsonDecode(jsonString) as Map<String, dynamic>;
      return decodedMap.map((key, value) => MapEntry(key, value.toString()));
    } catch (_) {
      throw Exception("Invalid Master Password or corrupted data payload.");
    } finally {
      secureWipe(passBytes);
      secureWipe(keyBytes);
    }
  }
}