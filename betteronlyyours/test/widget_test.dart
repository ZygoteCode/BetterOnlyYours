import 'package:betteronlyyours/app/theme/app_theme.dart';
import 'package:betteronlyyours/app/theme/tokens.dart';
import 'package:betteronlyyours/core/models/app_settings.dart';
import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/services/password_strength.dart';
import 'package:betteronlyyours/shared/widgets/app_button.dart';
import 'package:betteronlyyours/shared/widgets/empty_state.dart';
import 'package:betteronlyyours/shared/widgets/entry_avatar.dart';
import 'package:betteronlyyours/shared/widgets/highlighted_text.dart';
import 'package:betteronlyyours/shared/widgets/strength_meter.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget wrap(Widget child, {AppSettings settings = const AppSettings()}) {
  return MaterialApp(
    theme: AppTheme.build(settings),
    home: Scaffold(body: Center(child: child)),
  );
}

void main() {
  testWidgets('AppButton reports taps and blocks them while loading', (
    tester,
  ) async {
    var taps = 0;
    await tester.pumpWidget(
      wrap(AppButton(label: 'Save', onPressed: () => taps++)),
    );

    expect(find.text('Save'), findsOneWidget);
    await tester.tap(find.text('Save'));
    expect(taps, 1);

    await tester.pumpWidget(
      wrap(AppButton(label: 'Save', loading: true, onPressed: () => taps++)),
    );
    await tester.tap(find.byType(AppButton));
    expect(taps, 1);
  });

  testWidgets('EmptyState offers the next action', (tester) async {
    var pressed = false;
    await tester.pumpWidget(
      wrap(
        EmptyState(
          icon: Icons.lock_open_rounded,
          title: 'Your vault is empty',
          message: 'Create your first entry.',
          actionLabel: 'New entry',
          onAction: () => pressed = true,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Your vault is empty'), findsOneWidget);
    await tester.tap(find.text('New entry'));
    expect(pressed, isTrue);
  });

  testWidgets('StrengthMeter labels the estimate', (tester) async {
    await tester.pumpWidget(
      wrap(
        StrengthMeter(
          strength: PasswordStrength.evaluate(r'9Tz#vQ2m!Lk8@Rd4^Ws1&Xp7'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('bits'), findsOneWidget);
  });

  testWidgets('EntryAvatar shows deterministic initials', (tester) async {
    await tester.pumpWidget(
      wrap(const EntryAvatar(entry: VaultEntry(title: 'Home Server'))),
    );

    expect(find.text('HS'), findsOneWidget);
  });

  testWidgets('HighlightedText splits matched characters into spans', (
    tester,
  ) async {
    await tester.pumpWidget(
      wrap(
        const HighlightedText(
          text: 'GitHub',
          positions: <int>[0, 1, 2],
          style: TextStyle(fontSize: 14),
          highlightColor: Color(0xFF7C5CFF),
        ),
      ),
    );

    final richText = tester.widget<RichText>(find.byType(RichText));
    final leaves = <TextSpan>[];
    void visit(InlineSpan span) {
      if (span is! TextSpan) return;
      if (span.text != null) leaves.add(span);
      span.children?.forEach(visit);
    }

    visit(richText.text);
    expect(leaves.map((s) => s.text).join(), 'GitHub');
    expect(leaves.length, 2, reason: 'matched prefix is its own span');
    expect(leaves.first.style?.color, const Color(0xFF7C5CFF));
  });

  testWidgets('reduce-motion collapses every animation duration to zero', (
    tester,
  ) async {
    late AppTokens tokens;
    await tester.pumpWidget(
      wrap(
        Builder(
          builder: (context) {
            tokens = context.tokens;
            return const SizedBox.shrink();
          },
        ),
        settings: const AppSettings(reduceMotion: true),
      ),
    );

    expect(tokens.motion.enabled, isFalse);
    expect(tokens.motion.normal.inMilliseconds, lessThanOrEqualTo(1));
    expect(tokens.motion.page.inMilliseconds, lessThanOrEqualTo(1));
  });

  testWidgets('themes carry their own palette', (tester) async {
    late AppTokens midnight;
    late AppTokens obsidian;

    await tester.pumpWidget(
      wrap(
        Builder(
          builder: (context) {
            midnight = context.tokens;
            return const SizedBox.shrink();
          },
        ),
      ),
    );
    await tester.pumpWidget(
      wrap(
        Builder(
          builder: (context) {
            obsidian = context.tokens;
            return const SizedBox.shrink();
          },
        ),
        settings: const AppSettings(theme: AppThemeVariant.obsidian),
      ),
    );
    // MaterialApp cross-fades themes; wait for the switch to complete.
    await tester.pumpAndSettle();

    expect(midnight.color.accent, isNot(obsidian.color.accent));
    expect(midnight.color.background, isNot(obsidian.color.background));
  });
}
