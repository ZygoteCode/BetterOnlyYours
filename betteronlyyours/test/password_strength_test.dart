import 'package:betteronlyyours/core/services/password_strength.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('an empty password has its own state', () {
    final result = PasswordStrength.evaluate('');
    expect(result.level, StrengthLevel.empty);
    expect(result.entropyBits, 0);
  });

  test('short passwords rank very weak', () {
    expect(PasswordStrength.evaluate('abc').level, StrengthLevel.veryWeak);
  });

  test('well-known passwords are punished', () {
    final common = PasswordStrength.evaluate('password');
    expect(common.level, StrengthLevel.veryWeak);
    expect(common.suggestions, isNotEmpty);
  });

  test('long random passwords rank at the top', () {
    final result = PasswordStrength.evaluate(r'9Tz#vQ2m!Lk8@Rd4^Ws1&Xp7');
    expect(result.level, anyOf(StrengthLevel.strong, StrengthLevel.excellent));
    expect(result.entropyBits, greaterThan(80));
  });

  test('repetition counts for less than variety', () {
    final repeated = PasswordStrength.evaluate('aaaaaaaaaaaaaaaa');
    final varied = PasswordStrength.evaluate('a7q-Zm2xP0kR4wLe');
    expect(repeated.entropyBits, lessThan(varied.entropyBits));
  });

  test('sequences are discounted', () {
    final sequential = PasswordStrength.evaluate('abcdefghijklmnop');
    final random = PasswordStrength.evaluate('kqxeirmzwptdvbnh');
    expect(sequential.entropyBits, lessThan(random.entropyBits));
  });

  test('fraction stays inside 0..1', () {
    for (final password in <String>['a', 'abc123', 'x' * 200]) {
      final fraction = PasswordStrength.evaluate(password).fraction;
      expect(fraction, inInclusiveRange(0, 1));
    }
  });
}
