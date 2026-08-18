/// Result of matching a query against a candidate string.
class FuzzyMatch {
  const FuzzyMatch({required this.score, required this.positions});

  final int score;

  /// Indices in the candidate string that matched, for highlighting.
  final List<int> positions;
}

/// Small, allocation-light fuzzy matcher used by the command palette and the
/// vault list. Matching is case-insensitive, order-preserving and rewards
/// prefix and word-boundary hits so that typing "gh" finds "GitHub" before
/// "Flight Hub".
class FuzzySearch {
  const FuzzySearch._();

  /// Matches every whitespace-separated token of [query] against [text].
  /// Returns `null` when any token is missing.
  static FuzzyMatch? match(String query, String text) {
    final trimmed = query.trim();
    if (trimmed.isEmpty) {
      return const FuzzyMatch(score: 0, positions: <int>[]);
    }

    final tokens = trimmed.split(RegExp(r'\s+'));
    var total = 0;
    final positions = <int>[];

    for (final token in tokens) {
      final result = _matchToken(token.toLowerCase(), text);
      if (result == null) return null;
      total += result.score;
      positions.addAll(result.positions);
    }

    positions.sort();
    return FuzzyMatch(score: total, positions: positions);
  }

  static FuzzyMatch? _matchToken(String token, String text) {
    final haystack = text.toLowerCase();

    // Substring hits always beat scattered subsequence hits.
    final directIndex = haystack.indexOf(token);
    if (directIndex >= 0) {
      var score = 120 - directIndex.clamp(0, 40);
      if (directIndex == 0) score += 60;
      if (directIndex > 0 &&
          _isBoundary(haystack.codeUnitAt(directIndex - 1))) {
        score += 30;
      }
      if (token.length == haystack.length) score += 40;
      return FuzzyMatch(
        score: score + token.length * 3,
        positions: List<int>.generate(
          token.length,
          (i) => directIndex + i,
          growable: false,
        ),
      );
    }

    var textIndex = 0;
    var score = 0;
    var previousMatch = -2;
    final positions = <int>[];

    for (var i = 0; i < token.length; i++) {
      final needle = token.codeUnitAt(i);
      var found = -1;
      while (textIndex < haystack.length) {
        if (haystack.codeUnitAt(textIndex) == needle) {
          found = textIndex;
          break;
        }
        textIndex++;
      }
      if (found < 0) return null;

      score += 10;
      if (found == previousMatch + 1) score += 12;
      if (found == 0) score += 20;
      if (found > 0 && _isBoundary(haystack.codeUnitAt(found - 1))) score += 16;
      score -= (found - previousMatch - 1).clamp(0, 6);

      positions.add(found);
      previousMatch = found;
      textIndex++;
    }

    return FuzzyMatch(score: score, positions: positions);
  }

  static bool _isBoundary(int codeUnit) {
    // space, '-', '_', '.', '/', ':', '@'
    const boundaries = <int>{32, 45, 95, 46, 47, 58, 64};
    return boundaries.contains(codeUnit);
  }
}
