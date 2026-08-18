import 'dart:convert';

import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('legacy compatibility', () {
    test('plain text values become note-only entries', () {
      final entry = VaultEntry.fromStorage('Server', 'root / hunter2');

      expect(entry.notes, 'root / hunter2');
      expect(entry.isLegacyFormat, isTrue);
      expect(entry.username, isEmpty);
    });

    test('note-only entries are written back byte-identical', () {
      const raw = 'line one\nline two';
      final entry = VaultEntry.fromStorage('Notes', raw);

      expect(entry.toStorageValue(), raw);
    });

    test('editing only the notes keeps the legacy representation', () {
      final entry = VaultEntry.fromStorage(
        'Notes',
        'old',
      ).copyWith(notes: 'new');

      expect(entry.toStorageValue(), 'new');
      expect(entry.isLegacyFormat, isTrue);
    });

    test('adding structure upgrades the entry', () {
      final entry = VaultEntry.fromStorage(
        'Mail',
        'some note',
      ).copyWith(username: 'me@example.com');

      final stored = entry.toStorageValue();
      expect(stored.startsWith(VaultEntry.structuredPrefix), isTrue);

      final parsed = VaultEntry.fromStorage('Mail', stored);
      expect(parsed.username, 'me@example.com');
      expect(parsed.notes, 'some note');
    });

    test('malformed structured payloads are preserved as text', () {
      final entry = VaultEntry.fromStorage(
        'Broken',
        '${VaultEntry.structuredPrefix}{not json',
      );

      expect(entry.notes, '${VaultEntry.structuredPrefix}{not json');
      expect(entry.toStorageValue(), '${VaultEntry.structuredPrefix}{not json');
    });
  });

  group('structured entries', () {
    test('round trip keeps every field', () {
      final created = DateTime.fromMillisecondsSinceEpoch(1700000000000);
      final entry = VaultEntry(
        title: 'GitHub',
        username: 'octocat',
        password: 's3cret',
        url: 'https://github.com',
        notes: 'personal account',
        tags: const <String>['dev', 'work'],
        favorite: true,
        createdAt: created,
        updatedAt: created,
        customFields: const <VaultCustomField>[
          VaultCustomField(label: 'Recovery', value: '1234', secret: true),
        ],
      );

      final parsed = VaultEntry.fromStorage('GitHub', entry.toStorageValue());

      expect(parsed.username, 'octocat');
      expect(parsed.password, 's3cret');
      expect(parsed.url, 'https://github.com');
      expect(parsed.notes, 'personal account');
      expect(parsed.tags, <String>['dev', 'work']);
      expect(parsed.favorite, isTrue);
      expect(parsed.createdAt, created);
      expect(parsed.customFields.single.label, 'Recovery');
      expect(parsed.customFields.single.secret, isTrue);
    });

    test('unknown fields written by a newer version survive a round trip', () {
      final raw =
          '${VaultEntry.structuredPrefix}'
          '${jsonEncode(<String, dynamic>{
            'username': 'a',
            'futureField': <String, dynamic>{'nested': true},
          })}';

      final entry = VaultEntry.fromStorage('X', raw);
      expect(entry.extra['futureField'], <String, dynamic>{'nested': true});

      final reparsed = VaultEntry.fromStorage('X', entry.toStorageValue());
      expect(reparsed.extra['futureField'], <String, dynamic>{'nested': true});
    });

    test('host strips the scheme and www prefix', () {
      expect(
        const VaultEntry(title: 'A', url: 'https://www.example.com/login').host,
        'example.com',
      );
      expect(
        const VaultEntry(title: 'A', url: 'example.org').host,
        'example.org',
      );
      expect(const VaultEntry(title: 'A').host, isNull);
    });

    test('search text never contains the password', () {
      const entry = VaultEntry(
        title: 'Bank',
        username: 'me',
        password: 'topsecret',
        tags: <String>['money'],
      );

      expect(entry.searchText.contains('topsecret'), isFalse);
      expect(entry.searchText.contains('money'), isTrue);
    });

    test('touched() only stamps entries that carry structure', () {
      final legacy = VaultEntry.fromStorage('L', 'text').touched();
      expect(legacy.updatedAt, isNull);

      final structured = const VaultEntry(title: 'S', username: 'u').touched();
      expect(structured.updatedAt, isNotNull);
      expect(structured.createdAt, isNotNull);
    });
  });
}
