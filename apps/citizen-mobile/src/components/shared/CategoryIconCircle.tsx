import React from 'react';
import { View, StyleSheet } from 'react-native';
import {
  Zap,
  AlertTriangle,
  Droplets,
  Trash2,
  Volume2,
  Shield,
  Globe,
  Building,
} from 'lucide-react-native';
import { Colors, Radius } from '../../theme';

type CivicCategory =
  | 'electricity'
  | 'roads'
  | 'transport'
  | 'water'
  | 'environment'
  | 'safety'
  | 'governance'
  | 'infrastructure'
  | string;

interface CategoryIconCircleProps {
  category: CivicCategory;
  size?: 'md' | 'lg';
}

const ICON_SIZE   = { md: 20, lg: 24 } as const;
const CIRCLE_SIZE = { md: 40, lg: 48 } as const;

function CategoryIcon({ category, size }: { category: string; size: number }) {
  const props = { size, color: Colors.primaryForeground, strokeWidth: 2 } as const;
  switch (category) {
    case 'electricity':    return <Zap {...props} />;
    case 'roads':
    case 'transport':      return <AlertTriangle {...props} />;
    case 'water':          return <Droplets {...props} />;
    case 'environment':    return <Trash2 {...props} />;
    case 'safety':         return <Shield {...props} />;
    case 'governance':     return <Globe {...props} />;
    case 'infrastructure': return <Building {...props} />;
    default:               return <AlertTriangle {...props} />;
  }
}

export function CategoryIconCircle({ category, size = 'md' }: CategoryIconCircleProps) {
  const d = CIRCLE_SIZE[size];
  return (
    <View
      style={[styles.circle, { width: d, height: d, borderRadius: Radius.full }]}
    >
      <CategoryIcon category={category} size={ICON_SIZE[size]} />
    </View>
  );
}

const styles = StyleSheet.create({
  circle: {
    backgroundColor: Colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
