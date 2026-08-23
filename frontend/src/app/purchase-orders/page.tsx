"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Supplier = { id: string; name: string };
type Part = { id: string; sku: string; name: string; unitCost: number };
type PoList = {
  id: string;
  poNumber: string;
  supplierName?: string;
  status: string;
  createdAt: string;
};

export default function PurchaseOrdersPage() {
  const [items, setItems] = useState<PoList[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [parts, setParts] = useState<Part[]>([]);
  const [supplierId, setSupplierId] = useState("");
  const [partId, setPartId] = useState("");
  const [qty, setQty] = useState("1");
  const [unitCost, setUnitCost] = useState("0");
  const [statusFilter, setStatusFilter] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load(status = statusFilter) {
    const qs = status ? `&status=${encodeURIComponent(status)}` : "";
    const [orders, sup, partPage] = await Promise.all([
      api<Paged<PoList>>(`/api/v1/purchase-orders?pageSize=50${qs}`),
      api<Paged<Supplier>>("/api/v1/suppliers?pageSize=100"),
      api<Paged<Part>>("/api/v1/parts?pageSize=100"),
    ]);
    setItems(orders.items);
    setSuppliers(sup.items);
    setParts(partPage.items);
    if (!supplierId && sup.items[0]) setSupplierId(sup.items[0].id);
    if (!partId && partPage.items[0]) {
      setPartId(partPage.items[0].id);
      setUnitCost(String(partPage.items[0].unitCost));
    }
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const created = await api<{ id: string }>("/api/v1/purchase-orders", {
        method: "POST",
        body: JSON.stringify({
          supplierId,
          lines: [{ partId, quantityOrdered: Number(qty), unitCost: Number(unitCost) }],
        }),
      });
      window.location.href = `/purchase-orders/${created.id}`;
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Purchase orders</h1>
      <form onSubmit={onCreate} className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3">
        <select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} required className="rounded-md border border-slate-300 px-3 py-2 text-sm">
          <option value="">Supplier</option>
          {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        <select
          value={partId}
          onChange={(e) => {
            setPartId(e.target.value);
            const p = parts.find((x) => x.id === e.target.value);
            if (p) setUnitCost(String(p.unitCost));
          }}
          required
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">Part</option>
          {parts.map((p) => <option key={p.id} value={p.id}>{p.sku} — {p.name}</option>)}
        </select>
        <input type="number" min="0.01" step="0.01" value={qty} onChange={(e) => setQty(e.target.value)} placeholder="Qty" required className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <input type="number" min="0" step="0.01" value={unitCost} onChange={(e) => setUnitCost(e.target.value)} placeholder="Unit cost" required className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">Create draft PO</button>
      </form>
      <div className="mb-3">
        <select
          value={statusFilter}
          onChange={(e) => {
            const v = e.target.value;
            setStatusFilter(v);
            load(v).catch((err) => setError(err.message));
          }}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">All statuses</option>
          <option value="DRAFT">Draft</option>
          <option value="ORDERED">Ordered</option>
          <option value="PARTIALLY_RECEIVED">Partially received</option>
          <option value="RECEIVED">Received</option>
          <option value="CANCELLED">Cancelled</option>
        </select>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">PO #</th>
              <th className="px-4 py-3">Supplier</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Created</th>
            </tr>
          </thead>
          <tbody>
            {items.map((po) => (
              <tr key={po.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-mono">
                  <Link href={`/purchase-orders/${po.id}`} className="underline">{po.poNumber}</Link>
                </td>
                <td className="px-4 py-3">{po.supplierName ?? "—"}</td>
                <td className="px-4 py-3">{po.status}</td>
                <td className="px-4 py-3 font-mono text-xs">{new Date(po.createdAt).toLocaleString()}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-slate-500">No purchase orders yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
