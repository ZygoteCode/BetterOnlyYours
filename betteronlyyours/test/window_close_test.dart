import 'dart:async';

import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/memory_settings_service.dart';

/// Closing the window must feel instant.
///
/// Regression guard: the app used to sit frozen on screen for several seconds
/// while the pre-close flush and the platform teardown ran unbounded.
class _FakeShutdown implements WindowShutdown {
  _FakeShutdown({this.hangDestroy = false, this.failDestroy = false});

  final bool hangDestroy;
  final bool failDestroy;

  int hidden = 0;
  int destroyed = 0;
  int terminated = 0;

  @override
  Future<void> hideWindow() async => hidden++;

  @override
  Future<void> destroyWindow() {
    destroyed++;
    if (failDestroy) return Future<void>.error(StateError('no window'));
    if (hangDestroy) return Completer<void>().future;
    return Future<void>.value();
  }

  @override
  void terminateProcess() => terminated++;
}

void main() {
  // WindowController talks to window_manager when it is disposed, and the
  // plugin installs a method-call handler as soon as it is touched.
  TestWidgetsFlutterBinding.ensureInitialized();

  late SettingsController settings;

  setUp(() {
    settings = SettingsController(service: MemorySettingsService())..load();
  });

  tearDown(() => settings.dispose());

  WindowController controller(
    _FakeShutdown shutdown, {
    Duration flush = const Duration(milliseconds: 40),
    Duration destroy = const Duration(milliseconds: 40),
  }) {
    return WindowController(
      settings: settings,
      shutdown: shutdown,
      flushBudget: flush,
      destroyBudget: destroy,
    );
  }

  test('hides first, then flushes, then destroys', () async {
    final order = <String>[];
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);
    window.onBeforeClose = () async => order.add('flush');

    await window.closeNow();

    expect(shutdown.hidden, 1, reason: 'the window disappears immediately');
    expect(order, <String>['flush']);
    expect(shutdown.destroyed, 1);
    expect(shutdown.terminated, 0);
    window.dispose();
  });

  test('a flush that never finishes cannot hold the app open', () async {
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);
    window.onBeforeClose = () => Completer<void>().future;

    final stopwatch = Stopwatch()..start();
    await window.closeNow();
    stopwatch.stop();

    expect(shutdown.destroyed, 1);
    expect(
      stopwatch.elapsedMilliseconds,
      lessThan(2000),
      reason: 'the flush budget bounds the wait',
    );
    window.dispose();
  });

  test('a failing flush is not fatal', () async {
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);
    window.onBeforeClose = () async => throw StateError('disk full');

    await window.closeNow();

    expect(shutdown.destroyed, 1);
    window.dispose();
  });

  test('a stalled teardown ends the process anyway', () async {
    final shutdown = _FakeShutdown(hangDestroy: true);
    final window = controller(shutdown);

    unawaited(window.closeNow());
    await Future<void>.delayed(const Duration(milliseconds: 150));

    expect(shutdown.destroyed, 1);
    expect(shutdown.terminated, 1, reason: 'the watchdog fired');
    window.dispose();
  });

  test('a teardown that throws ends the process immediately', () async {
    final shutdown = _FakeShutdown(failDestroy: true);
    final window = controller(shutdown);

    await window.closeNow();

    expect(shutdown.terminated, 1);
    window.dispose();
  });

  test('the watchdog does not fire after a clean teardown', () async {
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);

    await window.closeNow();
    await Future<void>.delayed(const Duration(milliseconds: 120));

    expect(shutdown.terminated, 0);
    window.dispose();
  });

  test('closing twice runs the sequence once', () async {
    var flushes = 0;
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);
    window.onBeforeClose = () async => flushes++;

    await Future.wait<void>(<Future<void>>[
      window.closeNow(),
      window.closeNow(),
    ]);
    window.onWindowClose();

    expect(flushes, 1);
    expect(shutdown.destroyed, 1);
    window.dispose();
  });

  test('window events never lock the vault or block on the platform', () {
    final shutdown = _FakeShutdown();
    final window = controller(shutdown);

    // Minimising and blurring stay deliberately inert.
    window.onWindowMinimize();
    window.onWindowBlur();
    window.onWindowRestore();
    window.onWindowFocus();

    expect(window.isFocused, isTrue);
    expect(shutdown.destroyed, 0);
    expect(shutdown.terminated, 0);
    window.dispose();
  });
}
