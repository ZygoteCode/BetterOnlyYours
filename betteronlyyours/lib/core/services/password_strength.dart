import 'dart:math';

enum StrengthLevel {
  empty('—'),
  veryWeak('Very weak'),
  weak('Weak'),
  fair('Fair'),
  strong('Strong'),
  excellent('Excellent');

  const StrengthLevel(this.label);

  final String label;
}

class PasswordStrength {
  const PasswordStrength({
    required this.level,
    required this.entropyBits,
    this.suggestions = const <String>[],
  });

  final StrengthLevel level;
  final double entropyBits;
  final List<String> suggestions;

  /// 0..1, for meters.
  double get fraction => (entropyBits / 110).clamp(0.0, 1.0);

  /// Heuristic strength estimate.
  ///
  /// This is guidance, not a security proof: it approximates entropy from the
  /// character classes in use and discounts repetition, keyboard runs and
  /// well-known passwords. A real attacker model would need a full dictionary
  /// and pattern analysis.
  static PasswordStrength evaluate(String password) {
    if (password.isEmpty) {
      return const PasswordStrength(
        level: StrengthLevel.empty,
        entropyBits: 0,
        suggestions: <String>['Enter or generate a password.'],
      );
    }

    var pool = 0;
    final hasLower = password.contains(RegExp('[a-z]'));
    final hasUpper = password.contains(RegExp('[A-Z]'));
    final hasDigit = password.contains(RegExp('[0-9]'));
    final hasSymbol = password.contains(RegExp(r'[^A-Za-z0-9]'));
    if (hasLower) pool += 26;
    if (hasUpper) pool += 26;
    if (hasDigit) pool += 10;
    if (hasSymbol) pool += 33;
    if (pool == 0) pool = 26;

    // Characters that merely repeat or continue a run add less uncertainty.
    var effectiveLength = 1.0;
    for (var i = 1; i < password.length; i++) {
      final prev = password.codeUnitAt(i - 1);
      final current = password.codeUnitAt(i);
      if (current == prev) {
        effectiveLength += 0.35;
      } else if ((current - prev).abs() == 1) {
        effectiveLength += 0.6;
      } else {
        effectiveLength += 1.0;
      }
    }

    var entropy = effectiveLength * (log(pool) / ln2);

    final lower = password.toLowerCase();
    final suggestions = <String>[];

    for (final common in _commonPasswords) {
      if (lower == common) {
        entropy = min(entropy, 8);
        suggestions.add('This is one of the most guessed passwords.');
        break;
      }
      if (common.length >= 5 && lower.contains(common)) {
        entropy = min(entropy, entropy * 0.55);
        suggestions.add('Avoid common words like "$common".');
        break;
      }
    }

    if (_hasKeyboardRun(lower)) {
      entropy = min(entropy, entropy * 0.75);
      suggestions.add('Avoid keyboard runs such as "qwerty" or "12345".');
    }

    if (password.length < 12) {
      suggestions.add('Use at least 12 characters — length matters most.');
    }
    final missingClasses = <String>[
      if (!hasLower) 'lowercase',
      if (!hasUpper) 'uppercase',
      if (!hasDigit) 'digits',
      if (!hasSymbol) 'symbols',
    ];
    if (missingClasses.isNotEmpty && password.length < 20) {
      suggestions.add('Add ${missingClasses.join(', ')} for a bigger pool.');
    }

    final level = switch (entropy) {
      < 28 => StrengthLevel.veryWeak,
      < 45 => StrengthLevel.weak,
      < 64 => StrengthLevel.fair,
      < 90 => StrengthLevel.strong,
      _ => StrengthLevel.excellent,
    };

    if (level == StrengthLevel.excellent && suggestions.isEmpty) {
      suggestions.add('Strong enough to store and forget.');
    }

    return PasswordStrength(
      level: level,
      entropyBits: entropy,
      suggestions: suggestions,
    );
  }

  static bool _hasKeyboardRun(String value) {
    const runs = <String>[
      'qwerty',
      'asdf',
      'zxcv',
      '1234',
      'abcd',
      'password',
      'admin',
    ];
    return runs.any(value.contains);
  }

  static const List<String> _commonPasswords = <String>[
    'password',
    'passw0rd',
    'letmein',
    'welcome',
    'iloveyou',
    'dragon',
    'monkey',
    'sunshine',
    'princess',
    'football',
    'baseball',
    'qwerty',
    'abc123',
    '123456',
    '12345678',
    '1234567890',
    'admin',
    'master',
    'shadow',
    'superman',
    'trustno1',
  ];
}
