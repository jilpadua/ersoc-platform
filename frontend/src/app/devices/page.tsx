"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Device = {
  id: string;
  customerId: string;
  deviceType: string;
  brand: string;
  model: string;
  serialNumber?: string;
  imei?: string;
};

type Customer = { id: string; name: string };

export default function DevicesPage() {
  const [items, setItems] = useState<Device[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [form, setForm] = useState({
    customerId: "",
    deviceType: "Laptop",
    brand: "",
    model: "",
    serialNumber: "",
  });
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [devices, cust] = await Promise.all([
      api<Paged<Device>>("/api/v1/devices?pageSize=50"),
      api<Paged<Customer>>("/api/v1/customers?pageSize=100"),
    ]);
    setItems(devices.items);
    setCustomers(cust.items);
    if (!form.customerId && cust.items[0]) {
      setForm((f) => ({ ...f, customerId: cust.items[0].id }));
    }
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api("/api/v1/devices", { method: "POST", body: JSON.stringify(form) });
      setForm((f) => ({ ...f, brand: "", model: "", serialNumber: "" }));
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Devices</h1>
      <form onSubmit={onCreate} className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3">
        <select
          value={form.customerId}
          onChange={(e) => setForm({ ...form, customerId: e.target.value })}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          required
        >
          <option value="">Customer</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
        <input value={form.deviceType} onChange={(e) => setForm({ ...form, deviceType: e.target.value })} placeholder="Type" className="rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <input value={form.brand} onChange={(e) => setForm({ ...form, brand: e.target.value })} placeholder="Brand" className="rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <input value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} placeholder="Model" className="rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <input value={form.serialNumber} onChange={(e) => setForm({ ...form, serialNumber: e.target.value })} placeholder="Serial" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">Add device</button>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Device</th>
              <th className="px-4 py-3">Type</th>
              <th className="px-4 py-3">Serial / IMEI</th>
            </tr>
          </thead>
          <tbody>
            {items.map((d) => (
              <tr key={d.id} className="border-t border-slate-100">
                <td className="px-4 py-3">
                  <Link href={`/devices/${d.id}`} className="font-medium underline">
                    {d.brand} {d.model}
                  </Link>
                </td>
                <td className="px-4 py-3">{d.deviceType}</td>
                <td className="px-4 py-3 font-mono text-xs">{d.serialNumber ?? d.imei ?? "—"}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={3} className="px-4 py-8 text-center text-slate-500">No devices yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
