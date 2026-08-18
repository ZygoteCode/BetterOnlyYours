enum GeneratorMode { characters, passphrase }

/// Configuration for the password generator. Persisted with the app settings
/// so the generator remembers how the user likes to work.
class GeneratorOptions {
  const GeneratorOptions({
    this.mode = GeneratorMode.characters,
    this.length = 24,
    this.lowercase = true,
    this.uppercase = true,
    this.digits = true,
    this.symbols = true,
    this.avoidAmbiguous = false,
    this.words = 5,
    this.separator = '-',
    this.capitalizeWords = true,
    this.appendNumber = true,
  });

  static const int minLength = 6;
  static const int maxLength = 128;
  static const int minWords = 3;
  static const int maxWords = 12;

  final GeneratorMode mode;
  final int length;
  final bool lowercase;
  final bool uppercase;
  final bool digits;
  final bool symbols;
  final bool avoidAmbiguous;
  final int words;
  final String separator;
  final bool capitalizeWords;
  final bool appendNumber;

  bool get hasCharacterClass => lowercase || uppercase || digits || symbols;

  GeneratorOptions copyWith({
    GeneratorMode? mode,
    int? length,
    bool? lowercase,
    bool? uppercase,
    bool? digits,
    bool? symbols,
    bool? avoidAmbiguous,
    int? words,
    String? separator,
    bool? capitalizeWords,
    bool? appendNumber,
  }) {
    return GeneratorOptions(
      mode: mode ?? this.mode,
      length: (length ?? this.length).clamp(minLength, maxLength),
      lowercase: lowercase ?? this.lowercase,
      uppercase: uppercase ?? this.uppercase,
      digits: digits ?? this.digits,
      symbols: symbols ?? this.symbols,
      avoidAmbiguous: avoidAmbiguous ?? this.avoidAmbiguous,
      words: (words ?? this.words).clamp(minWords, maxWords),
      separator: separator ?? this.separator,
      capitalizeWords: capitalizeWords ?? this.capitalizeWords,
      appendNumber: appendNumber ?? this.appendNumber,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
    'mode': mode.name,
    'length': length,
    'lowercase': lowercase,
    'uppercase': uppercase,
    'digits': digits,
    'symbols': symbols,
    'avoidAmbiguous': avoidAmbiguous,
    'words': words,
    'separator': separator,
    'capitalizeWords': capitalizeWords,
    'appendNumber': appendNumber,
  };

  factory GeneratorOptions.fromJson(Map<String, dynamic> json) {
    const fallback = GeneratorOptions();
    return GeneratorOptions(
      mode: GeneratorMode.values.firstWhere(
        (m) => m.name == json['mode'],
        orElse: () => fallback.mode,
      ),
      length: _int(json['length'], fallback.length).clamp(minLength, maxLength),
      lowercase: _bool(json['lowercase'], fallback.lowercase),
      uppercase: _bool(json['uppercase'], fallback.uppercase),
      digits: _bool(json['digits'], fallback.digits),
      symbols: _bool(json['symbols'], fallback.symbols),
      avoidAmbiguous: _bool(json['avoidAmbiguous'], fallback.avoidAmbiguous),
      words: _int(json['words'], fallback.words).clamp(minWords, maxWords),
      separator: json['separator'] is String
          ? json['separator'] as String
          : fallback.separator,
      capitalizeWords: _bool(json['capitalizeWords'], fallback.capitalizeWords),
      appendNumber: _bool(json['appendNumber'], fallback.appendNumber),
    );
  }

  static int _int(Object? value, int fallback) =>
      value is int ? value : (value is num ? value.toInt() : fallback);

  static bool _bool(Object? value, bool fallback) =>
      value is bool ? value : fallback;
}
