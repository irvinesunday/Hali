import React, { useState } from 'react';
import {
  Modal,
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { X } from 'lucide-react-native';
import { Button } from '../common/Button';
import { Colors, FontFamily, FontSize, Spacing, Radius, Shadows } from '../../theme';
import { apiRequest } from '../../api/client';

type Rating = 'positive' | 'neutral' | 'negative';

const RATINGS: Array<{ value: Rating; emoji: string; label: string }> = [
  { value: 'negative', emoji: '😕', label: 'Not great' },
  { value: 'neutral',  emoji: '😐', label: 'Okay' },
  { value: 'positive', emoji: '😊', label: 'Good' },
];

interface FeedbackSheetProps {
  visible: boolean;
  onClose: () => void;
  screen: string;
  clusterId?: string;
}

export function FeedbackSheet({
  visible,
  onClose,
  screen,
  clusterId,
}: FeedbackSheetProps) {
  const [rating, setRating]         = useState<Rating | null>(null);
  const [text, setText]             = useState('');
  const [submitted, setSubmitted]   = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    if (!rating) return;
    setSubmitting(true);
    // apiRequest returns a Result and never throws — no try/catch needed.
    // Fire-and-forget UX: always show the success state regardless of outcome;
    // feedback failure must never block or alarm the user.
    await apiRequest('/v1/feedback', {
      method: 'POST',
      body: {
        rating,
        text: text.trim() || null,
        screen,
        clusterId: clusterId ?? null,
        platform: Platform.OS,
      },
    });
    setSubmitting(false);
    setSubmitted(true);
    setTimeout(() => {
      setSubmitted(false);
      setRating(null);
      setText('');
      onClose();
    }, 1200);
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <KeyboardAvoidingView
        style={styles.overlay}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <TouchableOpacity style={styles.backdrop} activeOpacity={1} onPress={onClose} />
        <View style={styles.sheet}>
          <View style={styles.header}>
            <Text style={styles.title}>
              {submitted ? 'Thanks for your feedback' : 'Share feedback'}
            </Text>
            <TouchableOpacity onPress={onClose} accessibilityLabel="Close">
              <X size={20} color={Colors.mutedForeground} />
            </TouchableOpacity>
          </View>

          {!submitted ? (
            <>
              <View style={styles.ratingRow}>
                {RATINGS.map((r) => (
                  <TouchableOpacity
                    key={r.value}
                    style={[
                      styles.ratingBtn,
                      rating === r.value && styles.ratingBtnSelected,
                    ]}
                    onPress={() => setRating(r.value)}
                    accessibilityLabel={r.label}
                  >
                    <Text style={styles.ratingEmoji}>{r.emoji}</Text>
                  </TouchableOpacity>
                ))}
              </View>

              <TextInput
                style={styles.input}
                placeholder="What's on your mind? (optional)"
                placeholderTextColor={Colors.faintForeground}
                value={text}
                onChangeText={(t) => setText(t.slice(0, 300))}
                multiline
                numberOfLines={3}
                maxLength={300}
              />
              <Text style={styles.charCount}>{text.length}/300</Text>

              <Button
                label="Submit"
                onPress={handleSubmit}
                disabled={!rating}
                loading={submitting}
              />
            </>
          ) : (
            <View style={styles.success}>
              <Text style={styles.successEmoji}>😊</Text>
            </View>
          )}
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay:  { flex: 1, justifyContent: 'flex-end' },
  backdrop: { ...StyleSheet.absoluteFillObject, backgroundColor: 'rgba(0,0,0,0.4)' },
  sheet: {
    backgroundColor: Colors.card,
    borderTopLeftRadius: Radius['2xl'],
    borderTopRightRadius: Radius['2xl'],
    padding: Spacing.lg,
    gap: Spacing.lg,
    ...Shadows.modal,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  ratingRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    gap: Spacing.xl,
  },
  ratingBtn: {
    width: 56,
    height: 56,
    borderRadius: Radius.full,
    backgroundColor: Colors.muted,
    alignItems: 'center',
    justifyContent: 'center',
  },
  ratingBtnSelected: {
    backgroundColor: Colors.primarySubtle,
    borderWidth: 2,
    borderColor: Colors.primary,
  },
  ratingEmoji: { fontSize: 28 },
  input: {
    backgroundColor: Colors.muted,
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: Colors.border,
    padding: Spacing.md,
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
    minHeight: 80,
    textAlignVertical: 'top',
  },
  charCount: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
    textAlign: 'right',
    marginTop: -Spacing.md,
  },
  success:      { alignItems: 'center', paddingVertical: Spacing['2xl'] },
  successEmoji: { fontSize: 48 },
});
