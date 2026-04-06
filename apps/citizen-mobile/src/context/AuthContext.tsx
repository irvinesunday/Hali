// apps/citizen-mobile/src/context/AuthContext.tsx
//
// Authentication state machine: unknown → unauthenticated | authenticated.
//
// Boot: restore session from SecureStore on mount.
// signIn: persist tokens + transition to authenticated.
// signOut: best-effort logout API call, clear SecureStore, transition to
//          unauthenticated.
//
// The API client calls the registered auth failure handler when a refresh
// cycle fails irrecoverably — this dispatches SIGN_OUT and the root layout
// redirects to the auth stack.

import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
} from 'react';
import * as SecureStore from 'expo-secure-store';
import { registerAuthFailureHandler } from '../api/client';
import { logout as logoutApi } from '../api/auth';
import { SECURE_STORE_KEYS } from '../config/constants';

// ─── Types ───────────────────────────────────────────────────────────────────

export type AuthStatus = 'unknown' | 'unauthenticated' | 'authenticated';

export interface AuthState {
  status: AuthStatus;
  accountId: string | null;
}

export interface SignInParams {
  accessToken: string;
  refreshToken: string;
  accountId: string;
}

export interface AuthContextValue {
  authState: AuthState;
  signIn: (params: SignInParams) => Promise<void>;
  signOut: () => Promise<void>;
}

// ─── Reducer ─────────────────────────────────────────────────────────────────

type AuthAction =
  | { type: 'RESTORE'; accountId: string }
  | { type: 'SIGN_IN'; accountId: string }
  | { type: 'SIGN_OUT' };

function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'RESTORE':
      return { status: 'authenticated', accountId: action.accountId };
    case 'SIGN_IN':
      return { status: 'authenticated', accountId: action.accountId };
    case 'SIGN_OUT':
      return { status: 'unauthenticated', accountId: null };
    default:
      return state;
  }
}

const initialState: AuthState = {
  status: 'unknown',
  accountId: null,
};

// ─── Context ─────────────────────────────────────────────────────────────────

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [authState, dispatch] = useReducer(authReducer, initialState);

  // Boot: restore session from SecureStore
  useEffect(() => {
    let cancelled = false;

    async function restoreSession(): Promise<void> {
      try {
        const [accessToken, accountId] = await Promise.all([
          SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN),
          SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID),
        ]);

        if (cancelled) return;

        if (accessToken !== null && accountId !== null) {
          dispatch({ type: 'RESTORE', accountId });
        } else {
          dispatch({ type: 'SIGN_OUT' });
        }
      } catch {
        if (!cancelled) {
          dispatch({ type: 'SIGN_OUT' });
        }
      }
    }

    void restoreSession();

    return () => {
      cancelled = true;
    };
  }, []);

  // Register auth failure handler with API client (once, on mount)
  useEffect(() => {
    registerAuthFailureHandler(() => {
      dispatch({ type: 'SIGN_OUT' });
    });
  }, []);

  const signIn = useCallback(
    async (params: SignInParams): Promise<void> => {
      await Promise.all([
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.ACCESS_TOKEN,
          params.accessToken,
        ),
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.REFRESH_TOKEN,
          params.refreshToken,
        ),
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.ACCOUNT_ID,
          params.accountId,
        ),
      ]);
      dispatch({ type: 'SIGN_IN', accountId: params.accountId });
    },
    [],
  );

  const signOut = useCallback(async (): Promise<void> => {
    // Best-effort logout — clear local session regardless of API outcome
    try {
      const stored = await SecureStore.getItemAsync(
        SECURE_STORE_KEYS.REFRESH_TOKEN,
      );
      if (stored !== null) {
        await logoutApi({ refreshToken: stored });
      }
    } catch {
      // Swallowed intentionally
    } finally {
      await Promise.all([
        SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN),
        SecureStore.deleteItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN),
        SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID),
      ]);
      dispatch({ type: 'SIGN_OUT' });
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ authState, signIn, signOut }),
    [authState, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ─── Hook (canonical) ────────────────────────────────────────────────────────

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return ctx;
}

// ─── Legacy hook (backward-compat shim) ──────────────────────────────────────
//
// TEMPORARY: The 3 out-of-scope screens (app/index.tsx, (app)/_layout.tsx,
// settings/account.tsx) still import `useAuthContext` and destructure `state`.
// Expose a shim that reshapes { authState } → { state } so those files keep
// compiling. Delete this when the final screen sub-session migrates to
// useAuth(). Legacy consumers only read state.status — no tokens exposed.

export interface LegacyAuthContextValue {
  state: {
    status: AuthStatus;
    accountId: string | null;
  };
  signIn: (params: SignInParams) => Promise<void>;
  signOut: () => Promise<void>;
}

export function useAuthContext(): LegacyAuthContextValue {
  const { authState, signIn, signOut } = useAuth();
  return { state: authState, signIn, signOut };
}
