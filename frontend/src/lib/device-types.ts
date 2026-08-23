export const DEVICE_TYPES = [
  "Laptop",
  "Desktop",
  "Phone",
  "Tablet",
  "TV",
  "Monitor",
  "Console",
  "Other",
] as const;

export type DeviceTypeOption = (typeof DEVICE_TYPES)[number];
