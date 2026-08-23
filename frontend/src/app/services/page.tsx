"use client";

import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Service = {
  id: string;
  name: string;
  defaultPrice: number;
  warrantyDays: number;
  isActive: boolean;
  categoryName?: string;
};

export default function ServicesPage() {
  const [items, setItems] = useState<Service[]>([]);
  const [form, setForm] = useState({ name: "", defaultPrice: "0", warrantyDays: "30" });
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const data = await api<Paged<Service>>("/api/v1/services?pageSize=100");
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api("/api/v1/services", {
        method: "POST",
        body: JSON.stringify({
          name: form.name,
          defaultPrice: Number(form.defaultPrice),
          warrantyDays: Number(form.warrantyDays),
          isActive: true,
        }),
      });
      setForm({ name: "", defaultPrice: "0", warrantyDays: "30" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Service catalog</h1>
      <form onSubmit={onCreate} className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-4">
        <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Service name" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <input required type="number" min="0" step="0.01" value={form.defaultPrice} onChange={(e) => setForm({ ...form, defaultPrice: e.target.value })} placeholder="Price" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <input required type="number" min="0" value={form.warrantyDays} onChange={(e) => setForm({ ...form, warrantyDays: e.target.value })} placeholder="Warranty days" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">Add service</button>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Price</th>
              <th className="px-4 py-3">Warranty</th>
              <th className="px-4 py-3">Active</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-medium">{s.name}</td>
                <td className="px-4 py-3 font-mono text-xs">₱{s.defaultPrice.toLocaleString()}</td>
                <td className="px-4 py-3">{s.warrantyDays} days</td>
                <td className="px-4 py-3">{s.isActive ? "Yes" : "No"}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-slate-500">No services yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
