// OTP Verification screen — Flow A step 2
import React, { useState, useRef } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
} from 'react-native';
import { useRouter, useLocalSearchParams } from 'expo-router';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import * as Notifications from 'expo-notifications';
import * as Crypto from 'expo-crypto';
import { Button } from '../../src/components/common/Button';
import { verifyOtp, requestOtp } from '../../src/api/auth';
import { useAuthContext } from '../../src/context/AuthContext';

export default function OtpVerificationScreen() {
  const router = useRouter();
  const { destination } = useLocalSearchParams<{ destination: string }>();
  const { signIn } = useAuthContext();

  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resent, setResent] = useState(false);

  const inputRef = useRef<TextInput>(null);

  async function getDeviceFingerprint(): Promise<string> {
    const raw = `${Device.osName}-${Device.osVersion}-${Device.modelName}-${Application.applicationId}`;
    return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, raw);
  }

  async function getExpoPushToken(): Promise<string | undefined> {
    try {
      const { status } = await Notifications.requestPermissionsAsync();
      if (status !== 'granted') return undefined;
      const token = await Notifications.getExpoPushTokenAsync();
      return token.data;
    } catch {
      return undefined;
    }
  }

  async function handleVerify() {
    if (code.length !== 6) return;
    setError(null);
    setLoading(true);
    try {
      const [deviceHash, expoPushToken] = await Promise.all([
        getDeviceFingerprint(),
        getExpoPushToken(),
      ]);

      const tokens = await verifyOtp({
        destination,
        otp: code,
        deviceFingerprintHash: deviceHash,
        platform: Device.osName ?? undefined,
        appVersion: Application.nativeApplicationVersion ?? undefined,
        expoPushToken,
      });

      // The verify endpoint returns accessToken + refreshToken.
      // Account ID is decoded from the JWT sub claim or returned by /v1/users/me.
      // For simplicity we extract from the JWT payload here.
      const accountId = extractSubFromJwt(tokens.accessToken) ?? '';

      await signIn({
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        accountId,
      });

      router.replace('/(app)/home');
    } catch {
      setError('Invalid or expired code. Please try again.');
      setCode('');
    } finally {
      setLoading(false);
    }
  }

  async function handleResend() {
    setResending(true);
    setResent(false);
    try {
      await requestOtp({ destination, authMethod: 'phone_otp' });
      setResent(true);
      setError(null);
    } catch {
      setError('Could not resend code. Please try again.');
    } finally {
      setResending(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        style={styles.flex}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.heading}>Enter the code</Text>
        <Text style={styles.sub}>
          We sent a 6-digit code to{' '}
          <Text style={styles.phone}>{destination}</Text>.
        </Text>

        <TextInput
          ref={inputRef}
          style={styles.codeInput}
          value={code}
          onChangeText={(v) => {
            setCode(v.replace(/\D/g, '').slice(0, 6));
            setError(null);
          }}
          placeholder="000000"
          placeholderTextColor="#d1d5db"
          keyboardType="number-pad"
          maxLength={6}
          autoFocus
          returnKeyType="done"
          onSubmitEditing={handleVerify}
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}
        {resent ? (
          <Text style={styles.resent}>Code resent successfully.</Text>
        ) : null}

        <Button
          label="Verify"
          onPress={handleVerify}
          loading={loading}
          disabled={code.length !== 6}
        />

        <Button
          label="Resend code"
          variant="ghost"
          onPress={handleResend}
          loading={resending}
          style={styles.resendBtn}
        />

        <Button
          label="Change number"
          variant="ghost"
          onPress={() => router.back()}
          style={styles.backBtn}
        />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

/** Extracts the `sub` claim from a JWT without verifying the signature. */
function extractSubFromJwt(token: string): string | null {
  try {
    const payload = token.split('.')[1];
    const decoded = JSON.parse(atob(payload)) as Record<string, unknown>;
    const sub =
      (decoded['sub'] as string | undefined) ??
      (decoded[
        'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
      ] as string | undefined);
    return sub ?? null;
  } catch {
    return null;
  }
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: '#fff' },
  content: { flexGrow: 1, padding: 24, gap: 16 },
  heading: { fontSize: 26, fontWeight: '700', color: '#111827', marginTop: 32 },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  phone: { fontWeight: '600', color: '#111827' },
  codeInput: {
    borderWidth: 2,
    borderColor: '#1a3a2f',
    borderRadius: 12,
    paddingHorizontal: 20,
    paddingVertical: 16,
    fontSize: 28,
    fontWeight: '700',
    color: '#111827',
    textAlign: 'center',
    letterSpacing: 12,
    backgroundColor: '#f9fafb',
  },
  error: { fontSize: 14, color: '#dc2626', textAlign: 'center' },
  resent: { fontSize: 14, color: '#166534', textAlign: 'center' },
  resendBtn: { marginTop: -4 },
  backBtn: {},
});
