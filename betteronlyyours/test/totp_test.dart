import 'dart:convert';
import 'dart:typed_data';

import 'package:betteronlyyours/core/security/base32.dart';
import 'package:betteronlyyours/core/security/totp.dart';
import 'package:flutter_test/flutter_test.dart';

/// RFC 4226 / RFC 6238 conformance plus the parsing around them.
void main() {
  Uint8List bytes(String value) => Uint8List.fromList(utf8.encode(value));

  // The seeds from RFC 6238 appendix B: the 20-byte ASCII seed, repeated to
  // the block size of the stronger hashes.
  final sha1Seed = bytes('12345678901234567890');
  final sha256Seed = bytes('12345678901234567890123456789012');
  final sha512Seed = bytes(
    '1234567890123456789012345678901234567890'
    '123456789012345678901234',
  );

  group('HOTP (RFC 4226)', () {
    // Appendix D, secret "12345678901234567890", six digits.
    const expected = <String>[
      '755224',
      '287082',
      '359152',
      '969429',
      '338314',
      '254676',
      '287922',
      '162583',
      '399871',
      '520489',
    ];

    test('matches the reference values for counters 0-9', () {
      for (var counter = 0; counter < expected.length; counter++) {
        expect(
          Totp.hotp(secret: sha1Seed, counter: counter),
          expected[counter],
          reason: 'counter $counter',
        );
      }
    });

    test('an empty secret is refused', () {
      expect(
        () => Totp.hotp(secret: Uint8List(0), counter: 1),
        throwsFormatException,
      );
    });

    test('digit counts change only the width, never the value', () {
      final eight = Totp.hotp(
        secret: sha1Seed,
        counter: 0,
        config: const TotpConfig(digits: 8),
      );
      expect(eight.length, 8);
      expect(eight.endsWith('755224'), isTrue);
      expect(
        Totp.hotp(
          secret: sha1Seed,
          counter: 0,
          config: const TotpConfig(digits: 7),
        ).length,
        7,
      );
    });
  });

  group('TOTP (RFC 6238)', () {
    // Appendix B: eight digits, thirty-second steps.
    const vectors = <int, List<String>>{
      59: <String>['94287082', '46119246', '90693936'],
      1111111109: <String>['07081804', '68084774', '25091201'],
      1111111111: <String>['14050471', '67062674', '99943326'],
      1234567890: <String>['89005924', '91819424', '93441116'],
      2000000000: <String>['69279037', '90698825', '38618901'],
      20000000000: <String>['65353130', '77737706', '47863826'],
    };

    test('matches the reference table for SHA1, SHA256 and SHA512', () {
      vectors.forEach((seconds, codes) {
        final at = DateTime.fromMillisecondsSinceEpoch(
          seconds * 1000,
          isUtc: true,
        );
        final pairs = <(Uint8List, TotpAlgorithm, String)>[
          (sha1Seed, TotpAlgorithm.sha1, codes[0]),
          (sha256Seed, TotpAlgorithm.sha256, codes[1]),
          (sha512Seed, TotpAlgorithm.sha512, codes[2]),
        ];
        for (final (secret, algorithm, expected) in pairs) {
          final code = Totp.generate(
            secret: secret,
            config: TotpConfig(algorithm: algorithm, digits: 8),
            at: at,
          );
          expect(
            code.value,
            expected,
            reason: '${algorithm.label} at $seconds',
          );
        }
      });
    });

    test('the counter and the code roll over together', () {
      final justBefore = DateTime.fromMillisecondsSinceEpoch(
        29999,
        isUtc: true,
      );
      final justAfter = DateTime.fromMillisecondsSinceEpoch(30000, isUtc: true);

      final before = Totp.generate(secret: sha1Seed, at: justBefore);
      final after = Totp.generate(secret: sha1Seed, at: justAfter);

      expect(before.counter, 0);
      expect(after.counter, 1);
      expect(before.value, isNot(after.value));
      expect(before.millisecondsRemaining, 1);
      expect(after.millisecondsRemaining, 30000);
      expect(after.progress, 1.0);
    });

    test('the countdown follows the configured period', () {
      final at = DateTime.fromMillisecondsSinceEpoch(45000, isUtc: true);
      expect(Totp.counterAt(at, 60), 0);
      expect(Totp.millisecondsRemainingAt(at, 60), 15000);
      expect(Totp.counterAt(at, 15), 3);
      expect(Totp.millisecondsRemainingAt(at, 15), 15000);
    });

    test('local and UTC clocks produce the same code', () {
      final utc = DateTime.fromMillisecondsSinceEpoch(
        1234567890000,
        isUtc: true,
      );
      final local = utc.toLocal();
      expect(
        Totp.generate(secret: sha1Seed, at: local).value,
        Totp.generate(secret: sha1Seed, at: utc).value,
      );
    });

    test('a different secret gives a different code', () {
      final at = DateTime.fromMillisecondsSinceEpoch(59000, isUtc: true);
      expect(
        Totp.generate(secret: sha1Seed, at: at).value,
        isNot(Totp.generate(secret: sha256Seed, at: at).value),
      );
    });

    test('codes are grouped for reading', () {
      expect(
        const TotpCode(
          value: '123456',
          counter: 1,
          period: 30,
          millisecondsRemaining: 1000,
        ).grouped,
        '123 456',
      );
      expect(
        const TotpCode(
          value: '1234567',
          counter: 1,
          period: 30,
          millisecondsRemaining: 1000,
        ).grouped,
        '123 4567',
      );
    });
  });

  group('Steam', () {
    const steam = TotpConfig(kind: TotpKind.steam);

    test('produces five characters of its own alphabet', () {
      final at = DateTime.fromMillisecondsSinceEpoch(
        1111111109000,
        isUtc: true,
      );
      final code = Totp.generate(secret: sha1Seed, config: steam, at: at);

      expect(code.value.length, 5);
      for (final char in code.value.split('')) {
        expect(Totp.steamAlphabet.contains(char), isTrue, reason: char);
      }
    });

    test('is deterministic and differs from the standard rendering', () {
      final at = DateTime.fromMillisecondsSinceEpoch(59000, isUtc: true);
      final first = Totp.generate(secret: sha1Seed, config: steam, at: at);
      final second = Totp.generate(secret: sha1Seed, config: steam, at: at);

      expect(first.value, second.value);
      expect(first.value, isNot(Totp.generate(secret: sha1Seed, at: at).value));
      expect(steam.effectiveDigits, 5);
    });
  });

  group('base32', () {
    test('decodes what services actually print', () {
      // "Hello!" followed by DE AD BE EF — the classic sample secret.
      final expected = Uint8List.fromList(<int>[
        0x48,
        0x65,
        0x6C,
        0x6C,
        0x6F,
        0x21,
        0xDE,
        0xAD,
        0xBE,
        0xEF,
      ]);
      for (final variant in <String>[
        'JBSWY3DPEHPK3PXP',
        'jbswy3dpehpk3pxp',
        'JBSW Y3DP EHPK 3PXP',
        'JBSW-Y3DP-EHPK-3PXP',
        'JBSWY3DPEHPK3PXP======',
      ]) {
        expect(Base32.decode(variant), expected, reason: variant);
      }
    });

    test('round-trips arbitrary bytes', () {
      final data = Uint8List.fromList(
        List<int>.generate(37, (i) => i * 7 % 256),
      );
      expect(Base32.decode(Base32.encode(data)), data);
      expect(Base32.decode(Base32.encode(data, padding: true)), data);
    });

    test('rejects impossible input', () {
      expect(() => Base32.decode(''), throwsFormatException);
      expect(() => Base32.decode('JBSWY3DP1'), throwsFormatException);
      expect(() => Base32.decode('A'), throwsFormatException);
      expect(Base32.tryDecode('nope!'), isNull);
      expect(Base32.tryDecode('JBSWY3DP'), isNotNull);
    });
  });

  group('otpauth links', () {
    test('parses issuer, account and parameters', () {
      final parsed = OtpAuthUri.parse(
        'otpauth://totp/ACME%20Co:alice%40example.com'
        '?secret=JBSWY3DPEHPK3PXP&issuer=ACME%20Co'
        '&algorithm=SHA256&digits=8&period=60',
      );

      expect(parsed.config.issuer, 'ACME Co');
      expect(parsed.config.account, 'alice@example.com');
      expect(parsed.config.algorithm, TotpAlgorithm.sha256);
      expect(parsed.config.digits, 8);
      expect(parsed.config.period, 60);
      expect(parsed.secret, Base32.decode('JBSWY3DPEHPK3PXP'));
    });

    test('falls back to the defaults every authenticator assumes', () {
      final parsed = OtpAuthUri.parse(
        'otpauth://totp/alice?secret=JBSWY3DPEHPK3PXP',
      );
      expect(parsed.config.algorithm, TotpAlgorithm.sha1);
      expect(parsed.config.digits, 6);
      expect(parsed.config.period, 30);
      expect(parsed.config.issuer, isEmpty);
      expect(parsed.config.account, 'alice');
    });

    test('recognises the Steam encoder', () {
      final parsed = OtpAuthUri.parse(
        'otpauth://totp/Steam:player?secret=JBSWY3DPEHPK3PXP&encoder=steam',
      );
      expect(parsed.config.kind, TotpKind.steam);
    });

    test('refuses links it cannot honour', () {
      expect(
        () => OtpAuthUri.parse('https://example.com'),
        throwsFormatException,
      );
      expect(
        () => OtpAuthUri.parse('otpauth://hotp/x?secret=JBSWY3DPEHPK3PXP'),
        throwsFormatException,
      );
      expect(() => OtpAuthUri.parse('otpauth://totp/x'), throwsFormatException);
      expect(
        () => OtpAuthUri.parse('otpauth://totp/x?secret=!!!'),
        throwsFormatException,
      );
    });

    test('clamps absurd parameters instead of failing', () {
      final parsed = OtpAuthUri.parse(
        'otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&digits=99&period=0',
      );
      expect(parsed.config.digits, TotpConfig.maxDigits);
      expect(parsed.config.period, TotpConfig.minPeriod);
    });

    test('accepts a bare secret as well as a link', () {
      final bare = OtpAuthUri.parseSecretOrUri('jbsw y3dp ehpk 3pxp');
      expect(bare.secret, Base32.decode('JBSWY3DPEHPK3PXP'));
      expect(bare.config, const TotpConfig());

      final withDefaults = OtpAuthUri.parseSecretOrUri(
        'JBSWY3DPEHPK3PXP',
        defaults: const TotpConfig(digits: 8, period: 60),
      );
      expect(withDefaults.config.digits, 8);
      expect(withDefaults.config.period, 60);
    });

    test('round-trips through its canonical form', () {
      const source =
          'otpauth://totp/ACME:alice?secret=JBSWY3DPEHPK3PXP&issuer=ACME'
          '&algorithm=SHA512&digits=8&period=60';
      final first = OtpAuthUri.parse(source);
      final second = OtpAuthUri.parse(first.toUriString());

      expect(second.secret, first.secret);
      expect(second.config, first.config);
    });
  });
}
