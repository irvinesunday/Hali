import React, { useState } from 'react';
import { TouchableOpacity, StyleSheet } from 'react-native';
import { MessageSquarePlus } from 'lucide-react-native';
import { Colors, Spacing } from '../../theme';
import { FeedbackSheet } from './FeedbackSheet';

interface FeedbackButtonProps {
  screen: string;
  clusterId?: string;
}

export function FeedbackButton({ screen, clusterId }: FeedbackButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <TouchableOpacity
        style={styles.button}
        onPress={() => setOpen(true)}
        accessibilityRole="button"
        accessibilityLabel="Give feedback"
      >
        <MessageSquarePlus size={18} color={Colors.mutedForeground} strokeWidth={2} />
      </TouchableOpacity>
      <FeedbackSheet
        visible={open}
        onClose={() => setOpen(false)}
        screen={screen}
        clusterId={clusterId}
      />
    </>
  );
}

const styles = StyleSheet.create({
  button: {
    position: 'absolute',
    bottom: 84,
    left: Spacing.lg,
    zIndex: 40,
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: Colors.muted,
    borderWidth: 1,
    borderColor: Colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
