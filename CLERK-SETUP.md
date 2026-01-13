# Clerk Setup Guide - Fixing Localhost URL Issue

If you can't add `http://localhost:5173` in the Clerk dashboard, this guide will help.

## The Problem

Clerk's dashboard may not show a field to add "Allowed Origins" or callback URLs for localhost during initial setup.

## Solution: Where to Configure URLs in Clerk

### Step 1: Go to Your Clerk Application

1. Log in to [https://dashboard.clerk.com](https://dashboard.clerk.com)
2. Select your application (e.g., "A2S Workout Tracker")

### Step 2: Configure Development URLs

#### Option A: Using Clerk's Development Instance (Recommended for Testing)

Clerk **automatically allows localhost** in development mode! You don't need to configure anything special.

1. In your Clerk dashboard, you should see two environments:
   - **Development** (uses `pk_test_...` key)
   - **Production** (uses `pk_live_...` key)

2. When using a `pk_test_...` key (which you have), localhost URLs are **automatically allowed**:
   - `http://localhost:5173`
   - `http://localhost:*`
   - `https://localhost:*`

3. **Your current key already works with localhost!**
   ```env
   VITE_CLERK_PUBLISHABLE_KEY=pk_test_Y29zbWljLXRyZWVmcm9nLTMwLmNsZXJrLmFjY291bnRzLmRldiQ
   ```

#### Option B: Configure URLs Explicitly (If Needed)

If you want to see or configure URLs explicitly:

1. Go to **Configure** → **Domains**
2. Look for **Development URLs** section
3. Localhost should be listed automatically

### Step 3: Configure OAuth Providers

To enable GitHub and Google login:

1. In Clerk dashboard, go to **Configure** → **SSO Connections** (or **Social Connections**)

2. **Enable GitHub**:
   - Click "Add connection" or toggle GitHub ON
   - Clerk handles all OAuth configuration automatically
   - No need to create GitHub OAuth app manually (Clerk provides one)
   - Just enable it and it works!

3. **Enable Google**:
   - Click "Add connection" or toggle Google ON
   - Same as GitHub - Clerk handles everything
   - Just enable and you're done!

### Step 4: Configure Redirect URLs (Production Only)

For **production** deployment (not needed for local development):

1. Go to **Configure** → **Domains**
2. Look for **Production URLs** or **Allowed Origins**
3. Add your production domain:
   ```
   https://yourdomain.com
   https://www.yourdomain.com
   ```

## Verifying Your Setup

### Test Localhost is Working

1. Make sure `.env.local` has your Clerk key:
   ```bash
   cd src/A2S.Web
   cat .env.local
   ```

2. Start the frontend:
   ```bash
   npm run dev
   ```

3. Open http://localhost:5173/sign-in

4. You should see Clerk's sign-in UI with:
   - Email/password fields
   - GitHub button (if enabled)
   - Google button (if enabled)

### If You See "Invalid Publishable Key" Error

This means the key is wrong or expired:

1. Go to Clerk dashboard → **API Keys**
2. Copy the **Publishable Key** (starts with `pk_test_...`)
3. Update `.env.local`:
   ```env
   VITE_CLERK_PUBLISHABLE_KEY=pk_test_your_actual_key_here
   ```
4. Restart dev server: `Ctrl+C` then `npm run dev`

### If OAuth Buttons Don't Show

1. Ensure OAuth providers are enabled in Clerk dashboard
2. Clear browser cache and reload
3. Check browser console for errors (F12)

## Testing Authentication Flow

Once configured, test the flow:

1. **Start the app**:
   ```bash
   npm start
   ```

2. **Open frontend**: http://localhost:5173

3. **Try to access dashboard**: http://localhost:5173/dashboard
   - Should redirect to /sign-in (you're not logged in)

4. **Sign up with email** or use **GitHub/Google OAuth**

5. **After login**: Should redirect to /dashboard

6. **Verify authentication works**:
   - You should see user info on dashboard
   - Clicking sign-out should log you out

## Running E2E Tests with Authentication

### Basic Tests (No Login Required)

These tests verify routing and UI without actual authentication:

```bash
# Run in headed mode to watch
npm run test:e2e:headed
```

You should see **8 tests pass**:
- ✅ Login page displays
- ✅ Sign-up page displays
- ✅ Unauthenticated redirects work
- ✅ Protected routes are blocked
- ✅ UI elements render correctly

### Full Login Tests (Requires Test User)

To enable the 7 skipped tests that actually log in:

1. **Create a test user in Clerk**:
   - Go to Clerk dashboard → **Users**
   - Click "Create user"
   - Email: `bigdave@gmail.com`
   - Password: `Tacomuncher123!`
   - (These are already in your `.env.test`)

2. **The tests should already be configured** with your credentials

3. **Remove `test.skip()` from tests** in `src/A2S.Web/tests/e2e/auth-login.spec.ts`:
   - Find tests with `test.skip(`
   - Change to `test(`
   - Save file

4. **Run tests**:
   ```bash
   npm run test:e2e:headed
   ```

5. **You should see actual login happen** in the browser!

## Common Issues

### "This application does not exist"

**Problem**: Clerk says application doesn't exist

**Solution**:
- Check you're using the correct publishable key
- Verify the key matches your application in dashboard
- Try copying the key again from Clerk dashboard

### OAuth buttons not appearing

**Problem**: Only email/password shows, no GitHub/Google

**Solution**:
1. Go to Clerk dashboard
2. Navigate to **Configure** → **Social Connections** or **SSO Connections**
3. Make sure GitHub and Google toggles are ON
4. Clear browser cache and reload

### Redirects not working

**Problem**: After login, doesn't redirect to dashboard

**Solution**:
1. Check `src/A2S.Web/src/features/auth/LoginPage.tsx`
2. Verify `afterSignInUrl="/dashboard"` is set
3. Check browser console for errors (F12)
4. Ensure app routing is configured correctly

## Summary

✅ **Clerk automatically allows localhost for development** - no special configuration needed!

✅ **Your current setup already works** - you have a `pk_test_...` key

✅ **To enable OAuth**: Just toggle GitHub/Google in Clerk dashboard

✅ **To run tests**: Use `npm run test:e2e:headed` to watch tests execute

## Need Help?

1. Check Clerk documentation: https://clerk.com/docs
2. Check test documentation: [src/A2S.Web/tests/e2e/README.md](src/A2S.Web/tests/e2e/README.md)
3. View running guide: [RUNNING.md](RUNNING.md)
