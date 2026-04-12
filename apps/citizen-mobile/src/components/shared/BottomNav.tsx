import React from 'react';
import { View, TouchableOpacity, Text, StyleSheet } from 'react-native';
import { Home, MapPin, User } from 'lucide-react-native';
import { Colors, FontFamily, FontSize, Spacing } from '../../theme';

export type NavTab = 'home' | 'areas' | 'profile';

interface BottomNavProps {
  activeTab: NavTab;
  onTabChange: (tab: NavTab) => void;
}

const TABS = [
  { id: 'home'    as NavTab, label: 'Home',       Icon: Home },
  { id: 'areas'   as NavTab, label: 'Your Areas', Icon: MapPin },
  { id: 'profile' as NavTab, label: 'Profile',    Icon: User },
] as const;

export function BottomNav({ activeTab, onTabChange }: BottomNavProps) {
  return (
    <View style={styles.nav}>
      {TABS.map(({ id, label, Icon }) => {
        const isActive = activeTab === id;
        return (
          <TouchableOpacity
            key={id}
            style={styles.tab}
            onPress={() => onTabChange(id)}
            activeOpacity={0.7}
            accessibilityRole="tab"
            accessibilityState={{ selected: isActive }}
            accessibilityLabel={label}
          >
            <Icon
              size={22}
              color={isActive ? Colors.primary : Colors.mutedForeground}
              strokeWidth={isActive ? 2.5 : 2}
            />
            <Text
              style={[styles.label, isActive ? styles.active : styles.inactive]}
            >
              {label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  nav: {
    flexDirection: 'row',
    height: 64,
    backgroundColor: Colors.background,
    borderTopWidth: 1,
    borderTopColor: Colors.border,
    paddingHorizontal: Spacing.lg,
  },
  tab: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 3,
  },
  label:    { fontSize: FontSize.micro },
  active:   { fontFamily: FontFamily.semiBold, color: Colors.primary },
  inactive: { fontFamily: FontFamily.medium,   color: Colors.mutedForeground },
});
