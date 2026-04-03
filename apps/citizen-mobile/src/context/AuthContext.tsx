import React, {
  createContext,
  useContext,
  useReducer,
  useEffect,
  useCallback,
} from 'react';
import * as SecureStore from 'expo-secure-store';
import { registerAuthFailureHandler } from '../api/client';
import { logout as apiLogout } from '../api/auth';
import { SECURE_STORE_KEYS } from '../config/constants';
import type { AuthState } from '../types/domain';

// ─── State machine ────────────────────────────────────────────────────────────
// UNKNOWN → check SecureStore
// UNAUTHENTICATED → /auth/phone
// AUTHENTICATED → /(app)/home
// 401 + refresh success → AUTHENTICATED (new tokens)
// 401 + refresh failure → UNAUTHENTICATED (tokens cleared)

type AuthAction =
  | { type: 'RESTORE'; payload: Omit<AuthState, 'status'> & { status: 'authenticated' | 'unauthenticated' } }
  | { type: 'SIGN_IN'; payload: { accessToken: string; refreshToken: string; accountId: string } }
  | { type: 'SIGN_OUT' };

function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'RESTORE':
      return { ...action.payload };
    case 'SIGN_IN':
      return {
        ...action.payload,
        status: 'authenticated',
      };
    case 'SIGN_OUT':
      return {
        accessToken: null,
        refreshToken: null,
        accountId: null,
        status: 'unauthenticated',
      };
  }
}

// ─── Context ──────────────────────────────────────────────────────────────────

interface AuthContextValue {
  state: AuthState;
  signIn: (tokens: {
    accessToken: string;
    refreshToken: string;
    accountId: string;
  }) => Promise<void>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, {
    accessToken: null,
    refreshToken: null,
    accountId: null,
    status: 'unknown',
  });

  // Boot: restore tokens from SecureStore
  useEffect(() => {
    async function bootstrap() {
      const [accessToken, refreshToken, accountId] = await Promise.all([
        SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN),
        SecureStore.getItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN),
        SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID),
      ]);

      if (refreshToken && accountId) {
        dispatch({
          type: 'RESTORE',
          payload: {
            accessToken,
            refreshToken,
            accountId,
            status: 'authenticated',
          },
        });
      } else {
        dispatch({
          type: 'RESTORE',
          payload: {
            accessToken: null,
            refreshToken: null,
            accountId: null,
            status: 'unauthenticated',
          },
        });
      }
    }

    bootstrap();
  }, []);

  // Register the API-layer auth-failure handler (called on refresh token expiry)
  const forceSignOut = useCallback(async () => {
    await Promise.all([
      SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN),
      SecureStore.deleteItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN),
      SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID),
    ]);
    dispatch({ type: 'SIGN_OUT' });
  }, []);

  useEffect(() => {
    registerAuthFailureHandler(forceSignOut);
  }, [forceSignOut]);

  const signIn = useCallback(
    async (tokens: {
      accessToken: string;
      refreshToken: string;
      accountId: string;
    }) => {
      await Promise.all([
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.ACCESS_TOKEN,
          tokens.accessToken,
        ),
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.REFRESH_TOKEN,
          tokens.refreshToken,
        ),
        SecureStore.setItemAsync(
          SECURE_STORE_KEYS.ACCOUNT_ID,
          tokens.accountId,
        ),
      ]);
      dispatch({ type: 'SIGN_IN', payload: tokens });
    },
    [],
  );

  const signOut = useCallback(async () => {
    const storedRefresh = await SecureStore.getItemAsync(
      SECURE_STORE_KEYS.REFRESH_TOKEN,
    );
    if (storedRefresh) {
      try {
        await apiLogout({ refreshToken: storedRefresh });
      } catch {
        // Best-effort — clear locally regardless
      }
    }
    await forceSignOut();
  }, [forceSignOut]);

  return (
    <AuthContext.Provider value={{ state, signIn, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuthContext must be used within AuthProvider');
  return ctx;
}
