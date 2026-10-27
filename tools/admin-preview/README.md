# Sample Admin view

From the repository root, run `.node/node.exe tools/admin-preview/server.mjs` and
open `http://127.0.0.1:3430/organization`. Stop that foreground process with Ctrl+C.
The server binds only to 127.0.0.1 and requires port 3430 to be free.

This composes the actual People screen, shared Administration navigation,
appearance and enterprise shell with a fictional identity and in-memory API.
It never proxies to a real API or provisions an account. Saves change only these
made-up records. Restart resets this sample, not the retained 3410 preview or the
real workspace on 3310. The sample token is a public fixture, not a credential.

Activity shows saved sample changes plus four labelled-sample scenarios through a
read-only adapter. It pages the original sample server response without restarting
or resetting saved data. Older sample-only action/type codes stay visible in Details
with honest unknown labels. The added examples are never saved to the server.
Storage and other workflow destinations are
deliberately unavailable here. Sign-out is a mock, not an identity-provider test.
Do not treat this as Microsoft sign-in, Next router or live backend evidence.
No new package installation is needed; Vite comes from the web test dependencies.
