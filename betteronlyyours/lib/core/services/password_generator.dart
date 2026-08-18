import 'dart:math';

import '../models/generator_options.dart';
import 'word_list.dart';

class GeneratedSecret {
  const GeneratedSecret({required this.value, required this.entropyBits});

  final String value;

  /// Entropy of the *generation process*, i.e. how much randomness went in.
  /// This is not a claim about how hard the result is to guess in a targeted
  /// attack, only about how it was produced.
  final double entropyBits;
}

class PasswordGenerator {
  PasswordGenerator({Random? random}) : _random = random ?? Random.secure();

  final Random _random;

  static const String lowercaseChars = 'abcdefghijklmnopqrstuvwxyz';
  static const String uppercaseChars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  static const String digitChars = '0123456789';
  static const String symbolChars = '!@#\$%^&*()-_=+[]{};:,.?/';

  /// Characters that are easy to confuse when read or transcribed.
  static const String ambiguousChars = 'Il1O0oB8S5Z2';

  GeneratedSecret generate(GeneratorOptions options) {
    return switch (options.mode) {
      GeneratorMode.characters => _generateCharacters(options),
      GeneratorMode.passphrase => _generatePassphrase(options),
    };
  }

  GeneratedSecret _generateCharacters(GeneratorOptions options) {
    final classes = <String>[];
    if (options.lowercase) classes.add(_filter(lowercaseChars, options));
    if (options.uppercase) classes.add(_filter(uppercaseChars, options));
    if (options.digits) classes.add(_filter(digitChars, options));
    if (options.symbols) classes.add(_filter(symbolChars, options));

    final usable = classes.where((c) => c.isNotEmpty).toList();
    if (usable.isEmpty) {
      // Never hand back an empty secret: fall back to the safest full class.
      usable.add(lowercaseChars + uppercaseChars + digitChars);
    }

    final pool = usable.join();
    final length = options.length.clamp(
      GeneratorOptions.minLength,
      GeneratorOptions.maxLength,
    );

    final chars = <String>[];
    // Guarantee at least one character from every enabled class when the
    // requested length allows it.
    for (final klass in usable) {
      if (chars.length >= length) break;
      chars.add(klass[_random.nextInt(klass.length)]);
    }
    while (chars.length < length) {
      chars.add(pool[_random.nextInt(pool.length)]);
    }
    _shuffle(chars);

    return GeneratedSecret(
      value: chars.join(),
      entropyBits: length * _log2(pool.length.toDouble()),
    );
  }

  GeneratedSecret _generatePassphrase(GeneratorOptions options) {
    final list = PassphraseWords.words;
    final count = options.words.clamp(
      GeneratorOptions.minWords,
      GeneratorOptions.maxWords,
    );

    final picked = List<String>.generate(count, (_) {
      final word = list[_random.nextInt(list.length)];
      return options.capitalizeWords ? _capitalize(word) : word;
    });

    var value = picked.join(
      options.separator.isEmpty ? '-' : options.separator,
    );
    var entropy = count * _log2(list.length.toDouble());

    if (options.appendNumber) {
      final number = _random.nextInt(100).toString().padLeft(2, '0');
      value =
          '$value${options.separator.isEmpty ? '' : options.separator}'
          '$number';
      entropy += _log2(100);
    }

    return GeneratedSecret(value: value, entropyBits: entropy);
  }

  String _filter(String source, GeneratorOptions options) {
    if (!options.avoidAmbiguous) return source;
    return source.split('').where((c) => !ambiguousChars.contains(c)).join();
  }

  void _shuffle(List<String> items) {
    for (var i = items.length - 1; i > 0; i--) {
      final j = _random.nextInt(i + 1);
      final temp = items[i];
      items[i] = items[j];
      items[j] = temp;
    }
  }

  static String _capitalize(String word) =>
      word.isEmpty ? word : word[0].toUpperCase() + word.substring(1);

  static double _log2(double value) => log(value) / ln2;
}
