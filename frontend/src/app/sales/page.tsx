"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { useEffect, useState } from "react";

type SaleList = {
  id: string;
  saleNumber: string;
  customerName?: string | null;
  status: string;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
  completedAt?: string | null;
};

export default function SalesPage() {
  const [items, setItems] = useState<SaleList[]>([]);
  const [status, setStatus] = useState("");
  const [unpaidOnly, setUnpaidOnly] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load(st = status, unpaid = unpaidOnly) {
    const qs = [
      "pageSize=50",
      st ? `status=${encodeURIComponent(st)}` : "",
      unpaid ? "unpaidOnly=true" : "",
    ]
      .filter(Boolean)
      .join("&");
    const data = await api<Paged<SaleList>>(`/api/v1/sales?${qs}`);
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Sales</h1>
        <Link
          href="/sales/new"
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
        >
          New sale
        </Link>
      </div>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">Status</label>
          <select
            value={status}
            onChange={(e) => {
              const v = e.target.value;
              setStatus(v);
              load(v, unpaidOnly).catch((err) => setError(err.message));
            }}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option value="">All</option>
            <option value="COMPLETED">Completed</option>
            <option value="VOIDED">Voided</option>
          </select>
        </div>
        <label className="flex items-center gap-2 pb-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={unpaidOnly}
            onChange={(e) => {
              const v = e.target.checked;
              setUnpaidOnly(v);
              load(status, v).catch((err) => setError(err.message));
            }}
          />
          Balance due only
        </label>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Sale #</th>
              <th className="px-4 py-3">Customer</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Total</th>
              <th className="px-4 py-3 text-right">Paid</th>
              <th className="px-4 py-3 text-right">Balance</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id} className="border-b border-slate-100">
                <td className="px-4 py-3">
                  <Link href={`/sales/${s.id}`} className="font-medium text-slate-900 underline">
                    {s.saleNumber}
                  </Link>
                </td>
                <td className="px-4 py-3">{s.customerName ?? "—"}</td>
                <td className="px-4 py-3">{s.status}</td>
                <td className="px-4 py-3 text-right font-mono">₱{s.totalAmount.toLocaleString()}</td>
                <td className="px-4 py-3 text-right font-mono">₱{s.amountPaid.toLocaleString()}</td>
                <td className="px-4 py-3 text-right font-mono">₱{s.balanceDue.toLocaleString()}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-500">
                  No sales yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
