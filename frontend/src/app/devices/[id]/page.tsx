"use client";

import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

type Device = {
  id: string;
  customerId: string;
  deviceType: string;
  brand: string;
  model: string;
  serialNumber?: string;
  imei?: string;
  condition?: string;
  accessories?: string;
};

type Repair = { id: string; repairNumber: string; statusName: string; deviceId: string };

export default function DeviceDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [device, setDevice] = useState<Device | null>(null);
  const [repairs, setRepairs] = useState<Repair[]>([]);

  useEffect(() => {
    if (!id) return;
    api<Device>(`/api/v1/devices/${id}`).then(setDevice);
    api<Paged<Repair>>(`/api/v1/repairs?pageSize=100`).then((d) =>
      setRepairs(d.items.filter((r) => r.deviceId === id))
    );
  }, [id]);

  if (!device) {
    return <AppShell><p className="text-sm text-slate-500">Loading…</p></AppShell>;
  }

  return (
    <AppShell>
      <h1 className="text-2xl font-semibold">{device.brand} {device.model}</h1>
      <p className="mt-1 text-sm text-slate-600">
        {device.deviceType}
        {device.serialNumber ? ` · SN ${device.serialNumber}` : ""}
        {device.imei ? ` · IMEI ${device.imei}` : ""}
      </p>
      <p className="mt-2 text-sm">
        <Link href={`/customers/${device.customerId}`} className="underline">View customer</Link>
      </p>
      {(device.condition || device.accessories) && (
        <div className="mt-4 rounded-md border border-slate-200 bg-white p-3 text-sm">
          {device.condition && <p>Condition: {device.condition}</p>}
          {device.accessories && <p>Accessories: {device.accessories}</p>}
        </div>
      )}
      <h2 className="mb-2 mt-8 text-sm font-semibold uppercase tracking-wide text-slate-500">Repair history</h2>
      <ul className="space-y-2">
        {repairs.map((r) => (
          <li key={r.id} className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
            <Link href={`/repairs/${r.id}`} className="font-mono underline">{r.repairNumber}</Link>
            <span className="ml-2">{r.statusName}</span>
          </li>
        ))}
        {repairs.length === 0 && <li className="text-sm text-slate-500">No repairs.</li>}
      </ul>
    </AppShell>
  );
}
