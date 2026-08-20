import 'package:material_ui/material_ui.dart';

/// Renders [text] with the characters at [positions] emphasised, so search
/// results show *why* they matched.
class HighlightedText extends StatelessWidget {
  const HighlightedText({
    super.key,
    required this.text,
    required this.positions,
    required this.style,
    required this.highlightColor,
    this.maxLines = 1,
    this.overflow = TextOverflow.ellipsis,
  });

  final String text;
  final List<int> positions;
  final TextStyle style;
  final Color highlightColor;
  final int maxLines;
  final TextOverflow overflow;

  @override
  Widget build(BuildContext context) {
    if (positions.isEmpty) {
      return Text(text, style: style, maxLines: maxLines, overflow: overflow);
    }

    final marked = positions.toSet();
    final spans = <TextSpan>[];
    final buffer = StringBuffer();
    var bufferHighlighted = marked.contains(0);

    void flush() {
      if (buffer.isEmpty) return;
      spans.add(
        TextSpan(
          text: buffer.toString(),
          style: bufferHighlighted
              ? style.copyWith(
                  color: highlightColor,
                  fontWeight: FontWeight.w700,
                )
              : style,
        ),
      );
      buffer.clear();
    }

    for (var i = 0; i < text.length; i++) {
      final highlighted = marked.contains(i);
      if (highlighted != bufferHighlighted) {
        flush();
        bufferHighlighted = highlighted;
      }
      buffer.write(text[i]);
    }
    flush();

    return Text.rich(
      TextSpan(children: spans),
      maxLines: maxLines,
      overflow: overflow,
    );
  }
}
