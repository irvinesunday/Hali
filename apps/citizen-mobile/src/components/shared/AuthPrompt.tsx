// Inline auth prompt displayed when an anonymous user attempts a protected
// action.  Renders as a small banner within the current screen — does not
// navigate away.  The caller decides whether to show it (e.g. based on
// isAuthenticated from AuthContext).

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { LogIn } from 'lucide-react-native';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../theme';

interface AuthPromptProps {
  /** Short explanation shown to the user, e.g. "Sign in to report a signal". */
  message: string;
  /** Called when the user taps the sign-in button. */
  onSignIn: () => void;
}

export function AuthPrompt({ message, onSignIn }: AuthPromptProps): React.ReactElement {
  return (
    <View style={styles.container} accessibilityRole="alert">
      <View style={styles.textRow}>
        <LogIn size={16} color={Colors.primary} strokeWidth={2} />
        <Text style={styles.message}>{message}</Text>
      </View>
      <TouchableOpacity
        style={styles.button}
        onPress={onSignIn}
        accessibilityRole="button"
        accessibilityLabel="Sign in"
      >
        <Text style={styles.buttonText}>Sign in</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: Colors.primarySubtle,
    borderRadius: Radius.lg,
    padding: Spacing.lg,
    gap: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.primary + '30',
  },
  textRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  message: {
    flex: 1,
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
    lineHeight: FontSize.body * 1.4,
  },
  button: {
    alignSelf: 'flex-start',
    backgroundColor: Colors.primary,
    paddingVertical: Spacing.sm,
    paddingHorizontal: Spacing.xl,
    borderRadius: Radius.md,
  },
  buttonText: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.primaryForeground,
  },
});
