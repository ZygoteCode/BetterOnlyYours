import 'package:betteronlyyours/core/services/fuzzy_search.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('an empty query matches everything with a neutral score', () {
    final match = FuzzySearch.match('   ', 'GitHub');
    expect(match, isNotNull);
    expect(match!.score, 0);
    expect(match.positions, isEmpty);
  });

  test('substring matches report their positions', () {
    final match = FuzzySearch.match('hub', 'GitHub');
    expect(match, isNotNull);
    expect(match!.positions, <int>[3, 4, 5]);
  });

  test('non-contiguous characters still match in order', () {
    final match = FuzzySearch.match('gh', 'GitHub');
    expect(match, isNotNull);
    expect(match!.positions, <int>[0, 3]);
  });

  test('out-of-order characters do not match', () {
    expect(FuzzySearch.match('hg', 'GitHub'), isNull);
    expect(FuzzySearch.match('zzz', 'GitHub'), isNull);
  });

  test('prefix matches outrank late matches', () {
    final prefix = FuzzySearch.match('git', 'GitHub')!;
    final late = FuzzySearch.match('git', 'My Git mirror')!;
    expect(prefix.score, greaterThan(late.score));
  });

  test('word-boundary matches outrank matches inside a word', () {
    final boundary = FuzzySearch.match('serv', 'home server')!;
    final inside = FuzzySearch.match('serv', 'observatory')!;
    expect(boundary.score, greaterThan(inside.score));
  });

  test('every whitespace-separated token must match', () {
    expect(FuzzySearch.match('git hub', 'GitHub mirror'), isNotNull);
    expect(FuzzySearch.match('git bank', 'GitHub mirror'), isNull);
  });

  test('matching is case-insensitive', () {
    expect(FuzzySearch.match('GITHUB', 'github.com'), isNotNull);
  });
}
