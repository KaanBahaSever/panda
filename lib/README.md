# lib/TSLib

`TSLib/` is the TeamSpeak client library from [Splamy/TS3AudioBot](https://github.com/Splamy/TS3AudioBot)
(commit `a69a38d8cba5a4d671dbe06505506f6b46f1d947`), licensed under the
[Open Software License 3.0](TSLib/LICENSE-OSL-3.0). Panda keeps a copy here because it needs a few changes
to talk to TeamSpeak 6 servers. All changes are in [`tslib-ts6.patch`](tslib-ts6.patch):

- **`Full/License.cs`** – TeamSpeak 5/6 servers add a new license block (type 8) to the handshake.
  After the usual 42-byte block header it holds 1 byte license type, 1 byte property count and then the
  properties, each as a 1-byte length followed by its data (same layout as
  [tsclientlib](https://github.com/ReSpeak/tsclientlib/blob/master/tsproto/src/license.rs)).
  Without this the client can't derive the shared secret and fails with
  `Invalid license block type 8`.
- **`Generated/M2B.cs`** – `initserver` also carries the client's own `client_nickname`; it no longer
  overwrites the server name.
- **`TSLib.csproj`** – builds only for `netstandard2.1`.

To re-apply the changes on a newer TSLib: `git apply tslib-ts6.patch` inside a TS3AudioBot checkout.
