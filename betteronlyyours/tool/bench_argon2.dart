// ignore_for_file: avoid_print
// Dev utility: measures Argon2id cost so the shipped parameters can be chosen
// from data instead of guesswork. Not part of the app.
import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

void main() {
  final salt = Uint8List.fromList(List<int>.generate(16, (i) => i));
  final password = Uint8List.fromList(
    utf8.encode('correct horse battery staple'),
  );

  for (final config in <List<int>>[
    <int>[19456, 2, 1],
    <int>[32768, 2, 1],
    <int>[47104, 1, 1],
    <int>[65536, 2, 1],
    <int>[65536, 3, 4],
    <int>[131072, 1, 1],
  ]) {
    final generator = Argon2BytesGenerator()
      ..init(
        Argon2Parameters(
          Argon2Parameters.ARGON2_id,
          salt,
          desiredKeyLength: 32,
          memory: config[0],
          iterations: config[1],
          lanes: config[2],
          version: Argon2Parameters.ARGON2_VERSION_13,
        ),
      );
    final out = Uint8List(32);
    final sw = Stopwatch()..start();
    generator.deriveKey(password, 0, out, 0);
    sw.stop();
    print(
      'm=${config[0]}KiB t=${config[1]} p=${config[2]} -> '
      '${sw.elapsedMilliseconds}ms',
    );
  }
}
