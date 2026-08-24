import 'dart:typed_data';

/// RFC 4648 base32, the encoding every authenticator uses for TOTP secrets.
///
/// Decoding is deliberately forgiving about presentation — issuers print
/// secrets lower-case, in groups of four, sometimes without padding — but
/// strict about content: an alphabet violation or an impossible length is an
/// error, never a silently mangled key.
class Base32 {
  const Base32._();

  static const String alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

  /// Bit lengths that cannot come out of a base32 encoder.
  static const Set<int> _invalidRemainders = <int>{1, 3, 6};

  static Uint8List decode(String input) {
    final cleaned = StringBuffer();
    for (final rune in input.runes) {
      final char = String.fromCharCode(rune);
      if (char == ' ' || char == '-' || char == '\t' || char == '\n') continue;
      if (char == '\r' || char == '_') continue;
      if (char == '=') break; // Padding: everything after it is padding too.
      cleaned.write(char.toUpperCase());
    }

    final symbols = cleaned.toString();
    if (symbols.isEmpty) {
      throw const FormatException('The secret is empty.');
    }
    if (_invalidRemainders.contains(symbols.length % 8)) {
      throw FormatException('Not a valid base32 length: ${symbols.length}.');
    }

    final out = Uint8List((symbols.length * 5) ~/ 8);
    var buffer = 0;
    var bits = 0;
    var index = 0;

    for (var i = 0; i < symbols.length; i++) {
      final value = alphabet.indexOf(symbols[i]);
      if (value < 0) {
        throw FormatException('"${symbols[i]}" is not a base32 character.');
      }
      buffer = (buffer << 5) | value;
      bits += 5;
      if (bits >= 8) {
        bits -= 8;
        out[index++] = (buffer >> bits) & 0xFF;
      }
    }

    // Whatever is left over must be zero padding; anything else means the
    // string was truncated mid-symbol.
    if (bits > 0 && (buffer & ((1 << bits) - 1)) != 0) {
      throw const FormatException('The secret ends in the middle of a byte.');
    }
    return out;
  }

  /// Returns null instead of throwing, for input the user is still typing.
  static Uint8List? tryDecode(String input) {
    try {
      return decode(input);
    } on FormatException {
      return null;
    }
  }

  static String encode(Uint8List data, {bool padding = false}) {
    if (data.isEmpty) return '';
    final out = StringBuffer();
    var buffer = 0;
    var bits = 0;

    for (final byte in data) {
      buffer = (buffer << 8) | byte;
      bits += 8;
      while (bits >= 5) {
        bits -= 5;
        out.write(alphabet[(buffer >> bits) & 0x1F]);
      }
    }
    if (bits > 0) {
      out.write(alphabet[(buffer << (5 - bits)) & 0x1F]);
    }
    if (padding) {
      while (out.length % 8 != 0) {
        out.write('=');
      }
    }
    return out.toString();
  }
}
