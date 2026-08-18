import 'package:betteronlyyours/core/models/vault_meta.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('round trips through storage', () {
    final created = DateTime.fromMillisecondsSinceEpoch(1600000000000);
    final meta = VaultMeta(
      createdAt: created,
      lastUsed: <String, DateTime>{'GitHub': created},
    );

    final parsed = VaultMeta.fromStorage(meta.toStorageValue());

    expect(parsed.createdAt, created);
    expect(parsed.lastUsed['GitHub'], created);
    expect(parsed.schemaVersion, VaultMeta.currentSchemaVersion);
  });

  test('unparsable metadata degrades to defaults instead of throwing', () {
    final parsed = VaultMeta.fromStorage('not json at all');
    expect(parsed.lastUsed, isEmpty);
  });

  test('access history is trimmed to the newest entries', () {
    var meta = const VaultMeta();
    for (var i = 0; i < 10; i++) {
      meta = meta.withAccess(
        'entry-$i',
        at: DateTime.fromMillisecondsSinceEpoch(1000 * i),
        keep: 5,
      );
    }

    expect(meta.lastUsed.length, 5);
    expect(meta.lastUsed.containsKey('entry-9'), isTrue);
    expect(meta.lastUsed.containsKey('entry-0'), isFalse);
  });

  test('renaming moves the usage timestamp', () {
    final at = DateTime.fromMillisecondsSinceEpoch(5000);
    final meta = const VaultMeta()
        .withAccess('old', at: at)
        .renamed('old', 'new');

    expect(meta.lastUsed['new'], at);
    expect(meta.lastUsed.containsKey('old'), isFalse);
  });
}
