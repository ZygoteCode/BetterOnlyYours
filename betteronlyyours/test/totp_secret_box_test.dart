import 'dart:convert';
import 'dart:typed_data';

import 'package:betteronlyyours/core/security/base32.dart';
import 'package:betteronlyyours/core/security/totp.dart';
import 'package:betteronlyyours/core/security/totp_secret_box.dart';
import 'package:betteronlyyours/core/security/vault_crypto.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late Uint8List contentKey;
  final secretText = 'JBSWY3DPEHPK3PXP';

  Uint8List secret() => Base32.decode(secretText);

  setUp(() => contentKey = TotpSecretBox.newContentKey());

  group('sealing', () {
    test('round-trips the secret and its settings', () {
      const config = TotpConfig(
        algorithm: TotpAlgorithm.sha512,
        digits: 8,
        period: 60,
        issuer: 'ACME',
        account: 'alice@example.com',
      );

      final sealed = TotpSecretBox.seal(
        secret: secret(),
        config: config,
        contentKey: contentKey,
      );
      final material = TotpSecretBox.open(sealed, contentKey);

      expect(material.secret, secret());
      expect(material.config, config);
      material.dispose();
    });

    test('never leaks the secret into the stored text', () {
      final sealed = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(issuer: 'ACME', account: 'alice'),
        contentKey: contentKey,
      );

      expect(sealed.startsWith(TotpSecretBox.prefix), isTrue);
      expect(sealed.contains(secretText), isFalse);
      expect(sealed.contains(base64.encode(secret())), isFalse);
      // Not even the labels are readable from the outside.
      expect(sealed.contains('ACME'), isFalse);
      expect(sealed.contains('alice'), isFalse);
    });

    test('two seals of the same secret look unrelated', () {
      final first = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );
      final second = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );

      expect(first, isNot(second));
      expect(
        TotpSecretBox.open(first, contentKey).secret,
        TotpSecretBox.open(second, contentKey).secret,
      );
    });

    test('an empty secret or a broken key is refused', () {
      expect(
        () => TotpSecretBox.seal(
          secret: Uint8List(0),
          config: const TotpConfig(),
          contentKey: contentKey,
        ),
        throwsA(isA<TotpSealException>()),
      );
      expect(
        () => TotpSecretBox.seal(
          secret: secret(),
          config: const TotpConfig(),
          contentKey: Uint8List(8),
        ),
        throwsA(isA<TotpSealException>()),
      );
    });
  });

  group('opening', () {
    test('the wrong content key cannot open a box', () {
      final sealed = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );
      final other = TotpSecretBox.newContentKey();

      expect(
        () => TotpSecretBox.open(sealed, other),
        throwsA(isA<TotpSealException>()),
      );
      expect(TotpSecretBox.tryOpen(sealed, other), isNull);
    });

    test('a tampered box is rejected, not silently mis-decrypted', () {
      final sealed = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );
      final parts = sealed.substring(TotpSecretBox.prefix.length).split('.');
      final payload = base64Url.decode(parts[2]);
      payload[0] ^= 0x01;
      final tampered =
          '${TotpSecretBox.prefix}${parts[0]}.${parts[1]}'
          '.${base64Url.encode(payload)}';

      expect(
        () => TotpSecretBox.open(tampered, contentKey),
        throwsA(isA<TotpSealException>()),
      );
    });

    test('a swapped salt is rejected: the key is bound to it', () {
      final first = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );
      final second = TotpSecretBox.seal(
        secret: secret(),
        config: const TotpConfig(),
        contentKey: contentKey,
      );
      final a = first.substring(TotpSecretBox.prefix.length).split('.');
      final b = second.substring(TotpSecretBox.prefix.length).split('.');

      expect(
        () => TotpSecretBox.open(
          '${TotpSecretBox.prefix}${b[0]}.${a[1]}.${a[2]}',
          contentKey,
        ),
        throwsA(isA<TotpSealException>()),
      );
    });

    test('garbage is recognised as such', () {
      expect(TotpSecretBox.looksSealed(null), isFalse);
      expect(TotpSecretBox.looksSealed(''), isFalse);
      expect(TotpSecretBox.looksSealed(TotpSecretBox.prefix), isFalse);
      expect(TotpSecretBox.tryOpen('plain text', contentKey), isNull);
      expect(
        () => TotpSecretBox.open('${TotpSecretBox.prefix}nope', contentKey),
        throwsA(isA<TotpSealException>()),
      );
      expect(
        () => TotpSecretBox.open('${TotpSecretBox.prefix}a.b.c', contentKey),
        throwsA(isA<TotpSealException>()),
      );
    });
  });

  group('material', () {
    test('generating a code wipes the secret behind it', () {
      final material = TotpSecretBox.open(
        TotpSecretBox.seal(
          secret: secret(),
          config: const TotpConfig(),
          contentKey: contentKey,
        ),
        contentKey,
      );

      final code = material.codeThenDispose(
        at: DateTime.fromMillisecondsSinceEpoch(59000, isUtc: true),
      );

      expect(code.value.length, 6);
      expect(material.isDisposed, isTrue);
      expect(material.secret.every((byte) => byte == 0), isTrue);
      // Disposing twice is harmless.
      material.dispose();
    });

    test('the code matches a direct computation from the same secret', () {
      final at = DateTime.fromMillisecondsSinceEpoch(
        1111111109000,
        isUtc: true,
      );
      const config = TotpConfig(algorithm: TotpAlgorithm.sha256, digits: 8);

      final sealed = TotpSecretBox.seal(
        secret: secret(),
        config: config,
        contentKey: contentKey,
      );
      final viaBox = TotpSecretBox.open(
        sealed,
        contentKey,
      ).codeThenDispose(at: at);

      expect(
        viaBox.value,
        Totp.generate(secret: secret(), config: config, at: at).value,
      );
    });

    test('never prints the secret', () {
      final material = TotpSecretBox.open(
        TotpSecretBox.seal(
          secret: secret(),
          config: const TotpConfig(),
          contentKey: contentKey,
        ),
        contentKey,
      );
      expect(material.toString().contains(secretText), isFalse);
      expect(material.toString(), contains('SHA1'));
      material.dispose();
    });
  });

  group('key derivation', () {
    test('HKDF gives every salt its own key', () {
      final salt = VaultCrypto.randomBytes(16);
      final same = VaultCrypto.hkdfSha256(
        key: contentKey,
        salt: salt,
        info: 'x',
      );
      final again = VaultCrypto.hkdfSha256(
        key: contentKey,
        salt: salt,
        info: 'x',
      );
      final otherSalt = VaultCrypto.hkdfSha256(
        key: contentKey,
        salt: VaultCrypto.randomBytes(16),
        info: 'x',
      );
      final otherInfo = VaultCrypto.hkdfSha256(
        key: contentKey,
        salt: salt,
        info: 'y',
      );

      expect(same, again);
      expect(same.length, VaultCrypto.keyLength);
      expect(same, isNot(otherSalt));
      expect(same, isNot(otherInfo));
    });

    test('content keys are full length and unpredictable', () {
      final first = TotpSecretBox.newContentKey();
      final second = TotpSecretBox.newContentKey();
      expect(first.length, TotpSecretBox.contentKeyLength);
      expect(first, isNot(second));
    });
  });
}
