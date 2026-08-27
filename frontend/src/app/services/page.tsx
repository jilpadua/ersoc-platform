"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Service = {
  id: string;
  name: string;
  description?: string;
  defaultPrice: number;
  warrantyDays: number;
  isActive: boolean;
  categoryName?: string;
};

const emptyForm = { name: "", description: "", defaultPrice: "", warrantyDays: "" };
const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function ServicesPage() {
  const [items, setItems] = useState<Service[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load(inactive = includeInactive) {
    const data = await api<Paged<Service>>(`/api/v1/services?pageSize=100&includeInactive=${inactive}`);
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  function startEdit(s: Service) {
    setEditingId(s.id);
    setForm({
      name: s.name,
      description: s.description ?? "",
      defaultPrice: String(s.defaultPrice),
      warrantyDays: String(s.warrantyDays),
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
        name: form.name,
        description: form.description || null,
        defaultPrice: Number(form.defaultPrice || 0),
        warrantyDays: Number(form.warrantyDays || 0),
        isActive: true,
      };
      if (editingId) {
        const existing = items.find((x) => x.id === editingId);
        await api(`/api/v1/services/${editingId}`, {
          method: "PATCH",
          body: JSON.stringify({ ...body, isActive: existing?.isActive ?? true }),
        });
      } else {
        await api("/api/v1/services", { method: "POST", body: JSON.stringify(body) });
      }
      cancelEdit();
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(s: Service) {
    setError(null);
    try {
      await api(`/api/v1/services/${s.id}/${s.isActive ? "deactivate" : "activate"}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Service catalog</h1>
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
      <form onSubmit={onSubmit} className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-5">
        <div>
          <label className={labelClass}>Name</label>
          <input
            required
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            placeholder="Service name"
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Description</label>
          <input
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            placeholder="Optional"
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Price</label>
          <input
            required
            type="number"
            min="0"
            step="0.01"
            value={form.defaultPrice}
            onChange={(e) => setForm({ ...form, defaultPrice: e.target.value })}
            placeholder="0.00"
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Warranty days</label>
          <input
            required
            type="number"
            min="0"
            step="1"
            value={form.warrantyDays}
            onChange={(e) => setForm({ ...form, warrantyDays: e.target.value })}
            placeholder="30"
            className={fieldClass}
          />
        </div>
        <div className="flex items-end gap-2">
          <button className="flex-1 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            {editingId ? "Save" : "Add service"}
          </button>
          {editingId && (
            <button type="button" onClick={cancelEdit} className="rounded-md border border-slate-300 px-3 py-2 text-sm">
              Cancel
            </button>
          )}
        </div>
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
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-medium">{s.name}</td>
                <td className="px-4 py-3 font-mono text-xs">₱{s.defaultPrice.toLocaleString()}</td>
                <td className="px-4 py-3">{s.warrantyDays} days</td>
                <td className="px-4 py-3">{s.isActive ? "Yes" : "No"}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button type="button" onClick={() => startEdit(s)} className="text-xs font-medium underline">
                      Edit
                    </button>
                    <button type="button" onClick={() => void toggleActive(s)} className="text-xs font-medium underline">
                      {s.isActive ? "Deactivate" : "Activate"}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-500">
                  No services yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
