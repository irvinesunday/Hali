-- Hali MVP seed taxonomy (expanded)
-- Ensure validator coverage for NLP outputs
--
-- AUTHORITY: This file supersedes 03_seed_taxonomy.sql for ALL taxonomy seeding.
-- Run this file only. It covers both taxonomy_categories AND taxonomy_conditions.
-- Do not run 03_seed_taxonomy.sql alongside this file.

INSERT INTO taxonomy_categories (category, subcategory_slug, display_name) VALUES
('roads', 'potholes', 'Potholes'),
('roads', 'flooding', 'Flooding'),
('roads', 'obstruction', 'Obstruction'),
('roads', 'road_damage', 'Road Damage'),
('roads', 'impassable_section', 'Impassable Section'),

('transport', 'matatu_obstruction', 'Matatu Obstruction'),
('transport', 'bus_stop_congestion', 'Bus Stop Congestion'),
('transport', 'lane_blocking', 'Lane Blocking'),
('transport', 'access_disruption', 'Access Disruption'),

('electricity', 'outage', 'Outage'),
('electricity', 'unstable_supply', 'Unstable Supply'),
('electricity', 'transformer_issue', 'Transformer Issue'),

('water', 'outage', 'Outage'),
('water', 'low_pressure', 'Low Pressure'),
('water', 'burst_pipe', 'Burst Pipe'),
('water', 'sewage_issue', 'Sewage Issue'),

('environment', 'illegal_dumping', 'Illegal Dumping'),
('environment', 'waste_overflow', 'Waste Overflow'),
('environment', 'public_noise', 'Public Noise'),
('environment', 'pollution', 'Pollution'),

('safety', 'exposed_hazard', 'Exposed Hazard'),
('safety', 'broken_streetlight', 'Broken Streetlight'),
('safety', 'unsafe_crossing', 'Unsafe Crossing'),

('governance', 'public_service_disruption', 'Public Service Disruption'),
('governance', 'blocked_access_public_facility', 'Blocked Access Public Facility'),

('infrastructure', 'broken_drainage', 'Broken Drainage'),
('infrastructure', 'damaged_footbridge', 'Damaged Footbridge'),
('infrastructure', 'damaged_public_asset', 'Damaged Public Asset')
ON CONFLICT (category, subcategory_slug) DO NOTHING;

-- Conditions: canonical condition slugs per category
-- These are the valid values the NLP layer will return as condition_level
INSERT INTO taxonomy_conditions (category, condition_slug, display_name, ordinal, is_positive) VALUES
-- roads
('roads', 'passable',   'Passable',   1, false),
('roads', 'difficult',  'Difficult',  2, false),
('roads', 'impassable', 'Impassable', 3, false),
-- water
('water', 'low_pressure', 'Low Pressure', 1, false),
('water', 'no_water',     'No Water',     2, false),
('water', 'restored',     'Restored',     3, true),
-- electricity
('electricity', 'intermittent', 'Intermittent', 1, false),
('electricity', 'no_power',     'No Power',     2, false),
('electricity', 'restored',     'Restored',     3, true),
-- transport
('transport', 'partial_obstruction', 'Partial Obstruction', 1, false),
('transport', 'major_obstruction',   'Major Obstruction',   2, false),
-- environment
('environment', 'light_accumulation', 'Light Accumulation', 1, false),
('environment', 'heavy_accumulation', 'Heavy Accumulation', 2, false),
-- safety
('safety', 'hazard_present', 'Hazard Present', 1, false),
('safety', 'hazard_cleared', 'Hazard Cleared', 1, true),
-- governance
('governance', 'service_unavailable', 'Service Unavailable', 1, false),
('governance', 'service_restored',    'Service Restored',    1, true),
-- infrastructure
('infrastructure', 'damage_observed', 'Damage Observed', 1, false),
('infrastructure', 'damage_severe',   'Damage Severe',   2, false)
ON CONFLICT (category, condition_slug) DO NOTHING;
