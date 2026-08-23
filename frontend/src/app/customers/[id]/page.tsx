"use client";

import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

type Customer = {
  id: string;
  name: string;
  email?: string;
  phone?: string;
  notes?: string;
};

type Device = {
  id: string;
  brand: string;
  model: string;
  serialNumber?: string;
  deviceType: string;
};

type Repair = {
  id: string;
  repairNumber: string;
  statusName: string;
  totalAmount: number;
  customerId: string;
};

export default function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [devices, setDevices] = useState<Device[]>([]);
  const [repairs, setRepairs] = useState<Repair[]>([]);

  useEffect(() => {
    if (!id) return;
    void Promise.all([
      api<Customer>(`/api/v1/customers/${id}`),
      api<Paged<Device>>(`/api/v1/customers/${id}/devices?pageSize=50`),
      api<Paged<Repair>>(`/api/v1/repairs?pageSize=100`),
    ]).then(([c, d, r]) => {
      setCustomer(c);
      setDevices(d.items);
      setRepairs(r.items.filter((x) => x.customerId === id));
    });
  }, [id]);

  if (!customer) {
    return (
      <AppShell>
        <p className="text-sm text-slate-500">Loading…</p>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <h1 className="text-2xl font-semibold">{customer.name}</h1>
      <p className="mt-1 text-sm text-slate-600">
        {customer.phone ?? "No phone"} · {customer.email ?? "No email"}
      </p>
      {customer.notes && (
        <p className="mt-3 rounded-md border border-slate-200 bg-white p-3 text-sm text-slate-700">
          {customer.notes}
        </p>
      )}

      <h2 className="mb-2 mt-8 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Devices
      </h2>
      <ul className="space-y-2">
        {devices.map((d) => (
          <li key={d.id} className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
            <Link href={`/devices/${d.id}`} className="font-medium underline">
              {d.brand} {d.model}
            </Link>
            <span className="ml-2 text-slate-500">
              {d.deviceType}
              {d.serialNumber ? ` · ${d.serialNumber}` : ""}
            </span>
          </li>
        ))}
        {devices.length === 0 && <li className="text-sm text-slate-500">No devices.</li>}
      </ul>

      <h2 className="mb-2 mt-8 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Repair history
      </h2>
      <ul className="space-y-2">
        {repairs.map((r) => (
          <li key={r.id} className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm">
            <Link href={`/repairs/${r.id}`} className="font-mono underline">
              {r.repairNumber}
            </Link>
            <span className="ml-2">{r.statusName}</span>
            <span className="ml-2 font-mono text-xs">₱{r.totalAmount}</span>
          </li>
        ))}
        {repairs.length === 0 && <li className="text-sm text-slate-500">No repairs.</li>}
      </ul>
    </AppShell>
  );
}
