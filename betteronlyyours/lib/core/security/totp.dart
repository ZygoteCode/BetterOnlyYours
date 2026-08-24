import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import 'base32.dart';

/// Hash used by the HMAC inside HOTP.
enum TotpAlgorithm {
  sha1('SHA1'),
  sha256('SHA256'),
  sha512('SHA512');

  const TotpAlgorithm(this.label);

  final String label;

  int get blockLength => switch (this) {
    TotpAlgorithm.sha1 => 64,
    TotpAlgorithm.sha256 => 64,
    TotpAlgorithm.sha512 => 128,
  };

  Digest createDigest() => switch (this) {
    TotpAlgorithm.sha1 => SHA1Digest(),
    TotpAlgorithm.sha256 => SHA256Digest(),
    TotpAlgorithm.sha512 => SHA512Digest(),
  };

  static TotpAlgorithm parse(String? raw, {TotpAlgorithm fallback = sha1}) {
    final value = raw?.trim().toUpperCase().replaceAll('-', '');
    return switch (value) {
      'SHA1' => TotpAlgorithm.sha1,
      'SHA256' => TotpAlgorithm.sha256,
      'SHA512' => TotpAlgorithm.sha512,
      _ => fallback,
    };
  }
}

/// Code flavour. Everything is RFC 6238 underneath; Steam only differs in how
/// the truncated value is rendered.
enum TotpKind {
  standard('totp'),
  steam('steam');

  const TotpKind(this.id);

  final String id;

  static TotpKind parse(String? raw) =>
      raw?.trim().toLowerCase() == 'steam' ? TotpKind.steam : TotpKind.standard;
}

/// Everything about a token except the secret itself.
class TotpConfig {
  const TotpConfig({
    this.algorithm = TotpAlgorithm.sha1,
    this.digits = defaultDigits,
    this.period = defaultPeriod,
    this.kind = TotpKind.standard,
    this.issuer = '',
    this.account = '',
  });

  static const int defaultDigits = 6;
  static const int defaultPeriod = 30;
  static const int minDigits = 4;
  static const int maxDigits = 10;
  static const int minPeriod = 5;
  static const int maxPeriod = 300;

  /// Digit counts offered in the interface. Any value in range still works
  /// when it arrives from an `otpauth://` URI.
  static const List<int> commonDigits = <int>[6, 7, 8];
  static const List<int> commonPeriods = <int>[15, 30, 60];

  final TotpAlgorithm algorithm;
  final int digits;
  final int period;
  final TotpKind kind;
  final String issuer;
  final String account;

  /// Steam codes are always five characters of its own alphabet.
  int get effectiveDigits => kind == TotpKind.steam ? 5 : digits;

  bool get isDefault =>
      algorithm == TotpAlgorithm.sha1 &&
      digits == defaultDigits &&
      period == defaultPeriod &&
      kind == TotpKind.standard;

  TotpConfig copyWith({
    TotpAlgorithm? algorithm,
    int? digits,
    int? period,
    TotpKind? kind,
    String? issuer,
    String? account,
  }) {
    return TotpConfig(
      algorithm: algorithm ?? this.algorithm,
      digits: digits ?? this.digits,
      period: period ?? this.period,
      kind: kind ?? this.kind,
      issuer: issuer ?? this.issuer,
      account: account ?? this.account,
    );
  }

  /// Clamps anything a hand-written URI may contain into workable ranges.
  TotpConfig normalized() {
    return TotpConfig(
      algorithm: algorithm,
      digits: digits.clamp(minDigits, maxDigits),
      period: period.clamp(minPeriod, maxPeriod),
      kind: kind,
      issuer: issuer.trim(),
      account: account.trim(),
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
    'alg': algorithm.label,
    'digits': digits,
    'period': period,
    if (kind != TotpKind.standard) 'kind': kind.id,
    if (issuer.isNotEmpty) 'issuer': issuer,
    if (account.isNotEmpty) 'account': account,
  };

  static TotpConfig fromJson(Map<String, dynamic> json) {
    return TotpConfig(
      algorithm: TotpAlgorithm.parse(json['alg'] as String?),
      digits: json['digits'] is int ? json['digits'] as int : defaultDigits,
      period: json['period'] is int ? json['period'] as int : defaultPeriod,
      kind: TotpKind.parse(json['kind'] as String?),
      issuer: json['issuer'] is String ? json['issuer'] as String : '',
      account: json['account'] is String ? json['account'] as String : '',
    ).normalized();
  }

  @override
  bool operator ==(Object other) =>
      other is TotpConfig &&
      other.algorithm == algorithm &&
      other.digits == digits &&
      other.period == period &&
      other.kind == kind &&
      other.issuer == issuer &&
      other.account == account;

  @override
  int get hashCode =>
      Object.hash(algorithm, digits, period, kind, issuer, account);
}

/// A generated code plus the timing the interface needs to count it down.
class TotpCode {
  const TotpCode({
    required this.value,
    required this.counter,
    required this.period,
    required this.millisecondsRemaining,
  });

  final String value;

  /// RFC 6238 time step this code belongs to. Changes exactly when the code
  /// does, which makes it the right thing to key a widget on.
  final int counter;
  final int period;
  final int millisecondsRemaining;

  int get secondsRemaining => (millisecondsRemaining / 1000).ceil();

  /// 1.0 right after a refresh, 0.0 at expiry.
  double get progress =>
      (millisecondsRemaining / (period * 1000)).clamp(0.0, 1.0);

  /// `123 456`, the way every authenticator groups digits for reading aloud.
  String get grouped {
    final text = value;
    if (text.length < 6) return text;
    if (text.length.isEven) {
      final half = text.length ~/ 2;
      return '${text.substring(0, half)} ${text.substring(half)}';
    }
    return '${text.substring(0, 3)} ${text.substring(3)}';
  }
}

/// RFC 4226 (HOTP) and RFC 6238 (TOTP).
///
/// The implementation deliberately keeps the secret in a caller-owned buffer:
/// nothing here caches it, and callers wipe it as soon as the code is out.
class Totp {
  const Totp._();

  /// Steam renders the same truncated value in its own alphabet.
  static const String steamAlphabet = '23456789BCDFGHJKMNPQRTVWXY';

  static int counterAt(DateTime time, int period) {
    final seconds = time.toUtc().millisecondsSinceEpoch ~/ 1000;
    return seconds ~/ period;
  }

  static int millisecondsRemainingAt(DateTime time, int period) {
    final millis = time.toUtc().millisecondsSinceEpoch;
    final windowMillis = period * 1000;
    return windowMillis - (millis % windowMillis);
  }

  /// The RFC 4226 one-time password for an explicit [counter].
  static String hotp({
    required Uint8List secret,
    required int counter,
    TotpConfig config = const TotpConfig(),
  }) {
    if (secret.isEmpty) {
      throw const FormatException('The secret is empty.');
    }

    final message = Uint8List(8);
    ByteData.sublistView(message).setUint64(0, counter);

    final mac = HMac(
      config.algorithm.createDigest(),
      config.algorithm.blockLength,
    )..init(KeyParameter(secret));
    final digest = mac.process(message);

    // Dynamic truncation (RFC 4226 §5.3).
    final offset = digest[digest.length - 1] & 0x0F;
    final binary =
        ((digest[offset] & 0x7F) << 24) |
        ((digest[offset + 1] & 0xFF) << 16) |
        ((digest[offset + 2] & 0xFF) << 8) |
        (digest[offset + 3] & 0xFF);

    if (config.kind == TotpKind.steam) {
      var value = binary;
      final out = StringBuffer();
      for (var i = 0; i < 5; i++) {
        out.write(steamAlphabet[value % steamAlphabet.length]);
        value = value ~/ steamAlphabet.length;
      }
      return out.toString();
    }

    final digits = config.digits.clamp(
      TotpConfig.minDigits,
      TotpConfig.maxDigits,
    );
    final modulus = _pow10(digits);
    return (binary % modulus).toString().padLeft(digits, '0');
  }

  /// The code valid at [at] (defaults to now).
  static TotpCode generate({
    required Uint8List secret,
    TotpConfig config = const TotpConfig(),
    DateTime? at,
  }) {
    final normalized = config.normalized();
    final time = at ?? DateTime.now();
    final counter = counterAt(time, normalized.period);
    return TotpCode(
      value: hotp(secret: secret, counter: counter, config: normalized),
      counter: counter,
      period: normalized.period,
      millisecondsRemaining: millisecondsRemainingAt(time, normalized.period),
    );
  }

  static int _pow10(int exponent) {
    var result = 1;
    for (var i = 0; i < exponent; i++) {
      result *= 10;
    }
    return result;
  }
}

/// What a scanned QR code or a pasted setup link contains.
class OtpAuthUri {
  const OtpAuthUri({required this.secret, required this.config});

  final Uint8List secret;
  final TotpConfig config;

  /// True for text that means to be an `otpauth://` link, however malformed.
  static bool looksLikeUri(String raw) =>
      raw.trimLeft().toLowerCase().startsWith('otpauth://');

  /// Parses `otpauth://totp/Issuer:account?secret=…&algorithm=…`.
  ///
  /// Throws [FormatException] with a message worth showing to the user.
  static OtpAuthUri parse(String raw) {
    final uri = Uri.tryParse(raw.trim());
    if (uri == null || uri.scheme.toLowerCase() != 'otpauth') {
      throw const FormatException('Not an otpauth:// link.');
    }

    final type = uri.host.toLowerCase();
    if (type == 'hotp') {
      throw const FormatException(
        'Counter-based (HOTP) links are not supported.',
      );
    }

    final params = uri.queryParameters;
    final secretText = params['secret'];
    if (secretText == null || secretText.trim().isEmpty) {
      throw const FormatException('The link carries no secret.');
    }

    // "Issuer:account" — the issuer part is optional and repeated in a query
    // parameter, which wins when both are present.
    var label = uri.path;
    if (label.startsWith('/')) label = label.substring(1);
    label = Uri.decodeComponent(label);
    var issuer = params['issuer']?.trim() ?? '';
    var account = label;
    final separator = label.indexOf(':');
    if (separator >= 0) {
      if (issuer.isEmpty) issuer = label.substring(0, separator).trim();
      account = label.substring(separator + 1).trim();
    }

    final config = TotpConfig(
      algorithm: TotpAlgorithm.parse(params['algorithm']),
      digits: int.tryParse(params['digits'] ?? '') ?? TotpConfig.defaultDigits,
      period: int.tryParse(params['period'] ?? '') ?? TotpConfig.defaultPeriod,
      kind: TotpKind.parse(params['encoder'] ?? params['kind']),
      issuer: issuer,
      account: account,
    ).normalized();

    return OtpAuthUri(secret: Base32.decode(secretText), config: config);
  }

  /// Reads either a bare base32 secret or a full `otpauth://` link.
  static OtpAuthUri parseSecretOrUri(String raw, {TotpConfig? defaults}) {
    if (looksLikeUri(raw)) return parse(raw);
    return OtpAuthUri(
      secret: Base32.decode(raw),
      config: (defaults ?? const TotpConfig()).normalized(),
    );
  }

  /// Rebuilds the canonical link. Only used for round-trip tests — the app
  /// never puts a secret back on screen.
  String toUriString() {
    final label = config.issuer.isEmpty
        ? config.account
        : '${config.issuer}:${config.account}';
    final query = <String, String>{
      'secret': Base32.encode(secret),
      if (config.issuer.isNotEmpty) 'issuer': config.issuer,
      'algorithm': config.algorithm.label,
      'digits': '${config.digits}',
      'period': '${config.period}',
      if (config.kind == TotpKind.steam) 'encoder': 'steam',
    };
    return Uri(
      scheme: 'otpauth',
      host: 'totp',
      path: '/${Uri.encodeComponent(label)}',
      queryParameters: query,
    ).toString();
  }

  @override
  String toString() => 'OtpAuthUri(${config.issuer}/${config.account})';
}

/// Convenience for callers that hold the secret as text.
Uint8List totpSecretFromText(String text) => Base32.decode(text);

/// UTF-8 helper kept next to the parser so tests can build fixtures without
/// pulling in `dart:convert` everywhere.
Uint8List utf8Bytes(String value) => Uint8List.fromList(utf8.encode(value));
