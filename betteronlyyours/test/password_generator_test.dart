import 'dart:math';

import 'package:betteronlyyours/core/models/generator_options.dart';
import 'package:betteronlyyours/core/services/password_generator.dart';
import 'package:betteronlyyours/core/services/word_list.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final generator = PasswordGenerator(random: Random(42));

  test('respects the requested length', () {
    for (final length in <int>[8, 24, 64, 128]) {
      final result = generator.generate(GeneratorOptions(length: length));
      expect(result.value.length, length);
    }
  });

  test('includes at least one character from each enabled class', () {
    for (var i = 0; i < 50; i++) {
      final result = generator.generate(const GeneratorOptions(length: 12));
      expect(result.value.contains(RegExp('[a-z]')), isTrue);
      expect(result.value.contains(RegExp('[A-Z]')), isTrue);
      expect(result.value.contains(RegExp('[0-9]')), isTrue);
      expect(result.value.contains(RegExp(r'[^A-Za-z0-9]')), isTrue);
    }
  });

  test('digits-only mode produces digits only', () {
    final result = generator.generate(
      const GeneratorOptions(
        length: 16,
        lowercase: false,
        uppercase: false,
        symbols: false,
      ),
    );
    expect(result.value, matches(RegExp(r'^[0-9]{16}$')));
  });

  test('never returns an empty secret when all classes are disabled', () {
    final result = generator.generate(
      const GeneratorOptions(
        length: 10,
        lowercase: false,
        uppercase: false,
        digits: false,
        symbols: false,
      ),
    );
    expect(result.value.length, 10);
    expect(result.entropyBits, greaterThan(0));
  });

  test('avoids look-alike characters when asked', () {
    for (var i = 0; i < 40; i++) {
      final result = generator.generate(
        const GeneratorOptions(length: 40, avoidAmbiguous: true),
      );
      for (final char in PasswordGenerator.ambiguousChars.split('')) {
        expect(result.value.contains(char), isFalse, reason: 'contains $char');
      }
    }
  });

  test('entropy grows with length and pool size', () {
    final short = generator.generate(const GeneratorOptions(length: 8));
    final long = generator.generate(const GeneratorOptions(length: 32));
    final narrow = generator.generate(
      const GeneratorOptions(
        length: 32,
        uppercase: false,
        digits: false,
        symbols: false,
      ),
    );

    expect(long.entropyBits, greaterThan(short.entropyBits));
    expect(long.entropyBits, greaterThan(narrow.entropyBits));
  });

  test('passphrase mode uses the word list and separator', () {
    final result = generator.generate(
      const GeneratorOptions(
        mode: GeneratorMode.passphrase,
        words: 5,
        separator: '-',
        appendNumber: false,
        capitalizeWords: false,
      ),
    );

    final parts = result.value.split('-');
    expect(parts.length, 5);
    for (final part in parts) {
      expect(PassphraseWords.words.contains(part), isTrue);
    }
  });

  test('passphrase can capitalize and append a two-digit number', () {
    final result = generator.generate(
      const GeneratorOptions(
        mode: GeneratorMode.passphrase,
        words: 4,
        separator: '.',
      ),
    );

    final parts = result.value.split('.');
    expect(parts.length, 5);
    expect(parts.last, matches(RegExp(r'^\d{2}$')));
    expect(parts.first[0], parts.first[0].toUpperCase());
  });

  test('the word list has no duplicates', () {
    expect(PassphraseWords.words.toSet().length, PassphraseWords.words.length);
    expect(PassphraseWords.words.length, greaterThan(500));
  });

  test('two generated secrets differ', () {
    final live = PasswordGenerator();
    expect(
      live.generate(const GeneratorOptions()).value,
      isNot(live.generate(const GeneratorOptions()).value),
    );
  });
}
