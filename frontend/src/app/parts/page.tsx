"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Part = {
  id: string;
  sku: string;
  name: string;
  description?: string;
  unitCost: number;
  reorderLevel: number;
  quantityOnHand: number;
  isActive: boolean;
};

const emptyForm = {
  sku: "",
  name: "",
  description: "",
  unitCost: "",
  reorderLevel: "",
};

const fieldClass = "rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function PartsPage() {
  const [items, setItems] = useState<Part[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [adjustId, setAdjustId] = useState<string | null>(null);
  const [adjustDelta, setAdjustDelta] = useState("1");
  const [adjustReason, setAdjustReason] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load(inactive = includeInactive) {
    const data = await api<Paged<Part>>(`/api/v1/parts?pageSize=100&includeInactive=${inactive}`);
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  function startEdit(p: Part) {
    setEditingId(p.id);
    setForm({
      sku: p.sku,
      name: p.name,
      description: p.description ?? "",
      unitCost: String(p.unitCost),
      reorderLevel: String(p.reorderLevel),
    });
  }

  function cancelEdit() {
    setEditingId(null);
    setForm(emptyForm);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const body = {
        sku: form.sku,
        name: form.name,
        description: form.description || null,
        unitCost: Number(form.unitCost || 0),
        reorderLevel: Number(form.reorderLevel || 0),
      };
      if (editingId) {
        await api(`/api/v1/parts/${editingId}`, { method: "PATCH", body: JSON.stringify(body) });
      } else {
        await api("/api/v1/parts", { method: "POST", body: JSON.stringify(body) });
      }
      cancelEdit();
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(p: Part) {
    setError(null);
    try {
      await api(`/api/v1/parts/${p.id}/${p.isActive ? "deactivate" : "activate"}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function submitAdjust(e: FormEvent) {
    e.preventDefault();
    if (!adjustId) return;
    setError(null);
    try {
      await api(`/api/v1/parts/${adjustId}/adjustments`, {
        method: "POST",
        body: JSON.stringify({ quantityDelta: Number(adjustDelta), reason: adjustReason || null }),
      });
      setAdjustId(null);
      setAdjustDelta("1");
      setAdjustReason("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Parts</h1>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(e) => {
              const v = e.target.checked;
              setIncludeInactive(v);
              load(v).catch((err) => setError(err.message));
            }}
          />
          Show inactive
        </label>
      </div>
      <form onSubmit={onSubmit} className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3">
        <div>
          <label className={labelClass}>SKU</label>
          <input value={form.sku} onChange={(e) => setForm({ ...form, sku: e.target.value })} placeholder="SKU" required className={fieldClass + " w-full"} />
        </div>
        <div>
          <label className={labelClass}>Name</label>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Name" required className={fieldClass + " w-full"} />
        </div>
        <div>
          <label className={labelClass}>Description</label>
          <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Optional" className={fieldClass + " w-full"} />
        </div>
        <div>
          <label className={labelClass}>Unit cost</label>
          <input
            type="number"
            step="0.01"
            min="0"
            value={form.unitCost}
            onChange={(e) => setForm({ ...form, unitCost: e.target.value })}
            placeholder="0.00"
            required
            className={fieldClass + " w-full"}
          />
        </div>
        <div>
          <label className={labelClass}>Reorder level</label>
          <input
            type="number"
            step="1"
            min="0"
            value={form.reorderLevel}
            onChange={(e) => setForm({ ...form, reorderLevel: e.target.value })}
            placeholder="0"
            required
            className={fieldClass + " w-full"}
          />
        </div>
        <div className="flex items-end gap-2">
          <button className="flex-1 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">{editingId ? "Save" : "Add part"}</button>
          {editingId && <button type="button" onClick={cancelEdit} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>}
        </div>
      </form>
      {adjustId && (
        <form onSubmit={submitAdjust} className="mb-4 flex flex-wrap items-end gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4">
          <div>
            <label className={labelClass}>Quantity delta</label>
            <input
              type="number"
              step="1"
              value={adjustDelta}
              onChange={(e) => setAdjustDelta(e.target.value)}
              required
              className={fieldClass}
            />
          </div>
          <div className="min-w-[180px] flex-1">
            <label className={labelClass}>Reason</label>
            <input value={adjustReason} onChange={(e) => setAdjustReason(e.target.value)} placeholder="Optional" className={fieldClass + " w-full"} />
          </div>
          <button className="rounded-md bg-slate-900 px-3 py-2 text-sm text-white">Post adjustment</button>
          <button type="button" onClick={() => setAdjustId(null)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>
        </form>
      )}
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">SKU</th>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">On hand</th>
              <th className="px-4 py-3">Reorder</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-mono text-xs">
                  <Link href={`/parts/${p.id}`} className="underline">{p.sku}</Link>
                </td>
                <td className="px-4 py-3">{p.name}</td>
                <td className={`px-4 py-3 font-mono ${p.quantityOnHand < p.reorderLevel ? "text-amber-700" : ""}`}>
                  {p.quantityOnHand}
                </td>
                <td className="px-4 py-3 font-mono">{p.reorderLevel}</td>
                <td className="px-4 py-3">{p.isActive ? "Active" : "Inactive"}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-2">
                    <button type="button" onClick={() => startEdit(p)} className="text-xs font-medium underline">Edit</button>
                    <button type="button" onClick={() => setAdjustId(p.id)} className="text-xs font-medium underline">Adjust</button>
                    <button type="button" onClick={() => void toggleActive(p)} className="text-xs font-medium underline">
                      {p.isActive ? "Deactivate" : "Activate"}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-slate-500">No parts yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
