"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Repair = {
  id: string;
  repairNumber: string;
  customerName: string;
  deviceLabel: string;
  statusCode: string;
  statusName: string;
  totalAmount: number;
  receivedAt: string;
};

type Customer = { id: string; name: string };
type Device = { id: string; customerId: string; brand: string; model: string };
type Status = { code: string; name: string };

export default function RepairsPage() {
  const [items, setItems] = useState<Repair[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [devices, setDevices] = useState<Device[]>([]);
  const [statuses, setStatuses] = useState<Status[]>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [form, setForm] = useState({
    customerId: "",
    deviceId: "",
    reportedProblem: "",
    estimateAmount: "",
  });
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const qs = statusFilter ? `&statusCode=${encodeURIComponent(statusFilter)}` : "";
    const [repairs, cust, dev, st] = await Promise.all([
      api<Paged<Repair>>(`/api/v1/repairs?pageSize=50${qs}`),
      api<Paged<Customer>>("/api/v1/customers?pageSize=100"),
      api<Paged<Device>>("/api/v1/devices?pageSize=100"),
      api<Status[]>("/api/v1/repairs/statuses"),
    ]);
    setItems(repairs.items);
    setCustomers(cust.items);
    setDevices(dev.items);
    setStatuses(st);
    if (!form.customerId && cust.items[0]) {
      setForm((f) => ({ ...f, customerId: cust.items[0].id }));
    }
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, [statusFilter]);

  const customerDevices = devices.filter((d) => d.customerId === form.customerId);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api("/api/v1/repairs", {
        method: "POST",
        body: JSON.stringify({
          customerId: form.customerId,
          deviceId: form.deviceId,
          reportedProblem: form.reportedProblem,
          estimateAmount: form.estimateAmount ? Number(form.estimateAmount) : null,
        }),
      });
      setForm((f) => ({ ...f, reportedProblem: "", estimateAmount: "", deviceId: "" }));
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Repairs</h1>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">All statuses</option>
          {statuses.map((s) => (
            <option key={s.code} value={s.code}>{s.name}</option>
          ))}
        </select>
      </div>

      <form onSubmit={onCreate} className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-2">
        <select required value={form.customerId} onChange={(e) => setForm({ ...form, customerId: e.target.value, deviceId: "" })} className="rounded-md border border-slate-300 px-3 py-2 text-sm">
          <option value="">Customer</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select required value={form.deviceId} onChange={(e) => setForm({ ...form, deviceId: e.target.value })} className="rounded-md border border-slate-300 px-3 py-2 text-sm">
          <option value="">Device</option>
          {customerDevices.map((d) => <option key={d.id} value={d.id}>{d.brand} {d.model}</option>)}
        </select>
        <input required value={form.reportedProblem} onChange={(e) => setForm({ ...form, reportedProblem: e.target.value })} placeholder="Reported problem" className="rounded-md border border-slate-300 px-3 py-2 text-sm md:col-span-2" />
        <input value={form.estimateAmount} onChange={(e) => setForm({ ...form, estimateAmount: e.target.value })} placeholder="Estimate amount" type="number" min="0" step="0.01" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">Create repair</button>
      </form>

      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Repair #</th>
              <th className="px-4 py-3">Customer</th>
              <th className="px-4 py-3">Device</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Total</th>
            </tr>
          </thead>
          <tbody>
            {items.map((r) => (
              <tr key={r.id} className="border-t border-slate-100">
                <td className="px-4 py-3">
                  <Link href={`/repairs/${r.id}`} className="font-mono underline">{r.repairNumber}</Link>
                </td>
                <td className="px-4 py-3">{r.customerName}</td>
                <td className="px-4 py-3">{r.deviceLabel}</td>
                <td className="px-4 py-3">
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium">{r.statusName}</span>
                </td>
                <td className="px-4 py-3 font-mono text-xs">₱{r.totalAmount.toLocaleString()}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">No repairs yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
