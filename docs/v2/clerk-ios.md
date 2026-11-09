# Clerk in an installed PWA (iOS standalone)

**Status (8 Nov 2026):** configuration reviewed and hardened; the on-device check (install from the
Vercel preview → sign in → force-quit → reopen) still needs a real iPhone. Record the result here.

## What usually breaks

1. **Redirect-based sign-in leaves the standalone window.** If Clerk sends the user to a hosted
   `accounts.<domain>` page, iOS opens it in Safari and the session cookie lands in Safari, not the
   installed app. Fix: keep sign-in and sign-up on **our origin** with path routing.
2. **Session lost after force-quit.** Standalone WebKit keeps `localStorage`/cookies for the app's
   origin, but third-party cookies are blocked, so any cross-site session cookie disappears.
   Clerk's `@clerk/clerk-react` stores its session JWT for the first-party origin, which survives.
3. **`start_url` outside the auth flow.** The manifest `start_url` must be a signed-in route that
   `ProtectedRoute` can redirect from, not `/sign-in`.

## How this repo is set up

- `LoginPage` / `SignUpPage` use `routing="path"` with `path="/sign-in"` and `/sign-up` and
  `fallbackRedirectUrl="/today"` — same origin, no hosted redirect.
- Manifest `start_url` is `/today` with `display: standalone` and `scope: /`.
- `UserButton` uses `afterSignOutUrl="/sign-in"` so sign-out lands back inside the app window.
- Clerk's **allowed origins** must include the production and preview origins
  (`https://99-strength.enjoeneer.dev` and the Vercel preview domain) in the Clerk dashboard.

## Device checklist

- [ ] Open the preview URL in Safari, Share → Add to Home Screen.
- [ ] Launch from the Home Screen; sign in; confirm no Safari window opens.
- [ ] Force-quit; reopen; still signed in.
- [ ] Rotate, background 10 minutes, reopen; still signed in.
- [ ] Sign out; land on `/sign-in` inside the app.

If step 3 fails, switch `ClerkProvider` to `routerPush`/`routerReplace` wired to React Router and set
`signInUrl="/sign-in"` and `signUpUrl="/sign-up"` on the provider, then retest. Document the outcome.
