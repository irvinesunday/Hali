// Phone Entry screen — Flow A step 1
import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Button } from '../../src/components/common/Button';
import { requestOtp } from '../../src/api/auth';
import { normalisePhone, isValidE164 } from '../../src/utils/validators';
import { DEFAULT_COUNTRY_CODE } from '../../src/config/env';

export default function PhoneEntryScreen() {
  const router = useRouter();
  const [phone, setPhone] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const normalisedPhone = normalisePhone(phone.trim(), DEFAULT_COUNTRY_CODE);
  const isValid = isValidE164(normalisedPhone);

  async function handleSendOtp() {
    if (!isValid) return;
    setError(null);
    setLoading(true);
    try {
      await requestOtp({ destination: normalisedPhone, authMethod: 'phone_otp' });
      router.push({ pathname: '/(auth)/otp', params: { destination: normalisedPhone } });
    } catch {
      setError('Failed to send OTP. Please check the number and try again.');
    } finally {
      setLoading(false);
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
        <View style={styles.top}>
          <Text style={styles.wordmark}>hali</Text>
        </View>

        <Text style={styles.heading}>Enter your phone number</Text>
        <Text style={styles.sub}>
          We'll send a one-time code to verify your number.
        </Text>

        <TextInput
          style={styles.input}
          value={phone}
          onChangeText={(v) => { setPhone(v); setError(null); }}
          placeholder="+254 7XX XXX XXX"
          placeholderTextColor="#9ca3af"
          keyboardType="phone-pad"
          autoComplete="tel"
          textContentType="telephoneNumber"
          returnKeyType="done"
          onSubmitEditing={handleSendOtp}
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <Button
          label="Send code"
          onPress={handleSendOtp}
          loading={loading}
          disabled={!isValid}
          style={styles.cta}
        />

        <Text style={styles.legal}>
          By continuing you agree to Hali's terms of service and privacy policy.
        </Text>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: '#fff' },
  content: { flexGrow: 1, padding: 24, gap: 16 },
  top: { marginBottom: 16 },
  wordmark: { fontSize: 28, fontWeight: '800', color: '#1a3a2f' },
  heading: { fontSize: 26, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  input: {
    borderWidth: 1.5,
    borderColor: '#d1d5db',
    borderRadius: 10,
    paddingHorizontal: 16,
    paddingVertical: 14,
    fontSize: 18,
    color: '#111827',
    backgroundColor: '#fff',
  },
  error: { fontSize: 14, color: '#dc2626' },
  cta: { marginTop: 4 },
  legal: {
    fontSize: 12,
    color: '#9ca3af',
    textAlign: 'center',
    lineHeight: 18,
    marginTop: 8,
  },
});
