"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";

type Line = {
  id: string;
  partId: string;
  partSku?: string;
  partName?: string;
  quantityOrdered: number;
  quantityReceived: number;
  unitCost: number;
};

type PoDetail = {
  id: string;
  poNumber: string;
  supplierName?: string;
  status: string;
  notes?: string;
  lines: Line[];
};

export default function PurchaseOrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [po, setPo] = useState<PoDetail | null>(null);
  const [recvQty, setRecvQty] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const data = await api<PoDetail>(`/api/v1/purchase-orders/${id}`);
    setPo(data);
    const next: Record<string, string> = {};
    for (const l of data.lines) {
      const remaining = l.quantityOrdered - l.quantityReceived;
      next[l.id] = remaining > 0 ? String(remaining) : "0";
    }
    setRecvQty(next);
  }

  useEffect(() => {
    if (!id) return;
    load().catch((e) => setError(e instanceof ApiClientError ? e.message : e.message));
  }, [id]);

  async function action(path: string) {
    setError(null);
    try {
      await api(`/api/v1/purchase-orders/${id}/${path}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function receive(e: FormEvent) {
    e.preventDefault();
    if (!po) return;
    setError(null);
    try {
      const lines = po.lines
        .map((l) => ({ lineId: l.id, quantity: Number(recvQty[l.id] || 0) }))
        .filter((l) => l.quantity > 0);
      await api(`/api/v1/purchase-orders/${id}/receive`, {
        method: "POST",
        body: JSON.stringify({ lines }),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  if (!po) {
    return <AppShell><p className="text-sm text-slate-500">Loading…</p></AppShell>;
  }

  const canReceive = po.status === "ORDERED" || po.status === "PARTIALLY_RECEIVED";

  return (
    <AppShell>
      <Link href="/purchase-orders" className="text-sm text-slate-500 underline">← Purchase orders</Link>
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <h1 className="font-mono text-2xl font-semibold">{po.poNumber}</h1>
        <span className="rounded bg-slate-900 px-2 py-1 text-xs text-white">{po.status}</span>
      </div>
      <p className="mt-1 text-sm text-slate-600">{po.supplierName}</p>
      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}

      <div className="mt-4 flex flex-wrap gap-2">
        {po.status === "DRAFT" && (
          <>
            <button type="button" onClick={() => void action("submit")} className="rounded-md bg-slate-900 px-3 py-2 text-sm text-white">Submit order</button>
            <button type="button" onClick={() => void action("cancel")} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>
          </>
        )}
        {po.status === "ORDERED" && (
          <button type="button" onClick={() => void action("cancel")} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>
        )}
      </div>

      <section className="mt-6 rounded-lg border border-slate-200 bg-white p-4">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Lines</h2>
        <table className="w-full text-left text-sm">
          <thead className="text-xs uppercase text-slate-500">
            <tr>
              <th className="py-2">Part</th>
              <th className="py-2">Ordered</th>
              <th className="py-2">Received</th>
              <th className="py-2">Unit cost</th>
              {canReceive && <th className="py-2">Receive now</th>}
            </tr>
          </thead>
          <tbody>
            {po.lines.map((l) => (
              <tr key={l.id} className="border-t border-slate-100">
                <td className="py-2">{l.partSku} — {l.partName}</td>
                <td className="py-2 font-mono">{l.quantityOrdered}</td>
                <td className="py-2 font-mono">{l.quantityReceived}</td>
                <td className="py-2 font-mono">₱{l.unitCost}</td>
                {canReceive && (
                  <td className="py-2">
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      value={recvQty[l.id] ?? "0"}
                      onChange={(e) => setRecvQty({ ...recvQty, [l.id]: e.target.value })}
                      className="w-24 rounded-md border border-slate-300 px-2 py-1 text-sm"
                    />
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
        {canReceive && (
          <form onSubmit={receive} className="mt-4">
            <button className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white">Receive stock</button>
          </form>
        )}
      </section>
    </AppShell>
  );
}
