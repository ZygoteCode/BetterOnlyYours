// Dev utility: creates a demo vault so the desktop build can be exercised
// end to end. Not shipped with the app.
import 'dart:io';

import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';

Future<void> main(List<String> args) async {
  if (args.length < 2) {
    stderr.writeln('usage: make_demo_vault <path> <password>');
    exit(64);
  }
  final repository = VaultRepository(path: args[0], deriveOnIsolate: false);
  final entries = <String, String>{
    for (final name in <String>['GitHub', 'Bank', 'Home server', 'Mail'])
      name: VaultEntry.create(name)
          .copyWith(
            username: '${name.toLowerCase().split(' ').first}@example.com',
            password: 'Tz9#vQ2m!Lk8@Rd4-$name',
            url: 'https://${name.toLowerCase().split(' ').first}.example.com',
            tags: <String>['demo'],
            favorite: name == 'GitHub',
          )
          .toStorageValue(),
  };
  final session = await repository.create(args[1], entries: entries);
  session.dispose();
  stdout.writeln('created ${args[0]}');
}
