"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Supplier = {
  id: string;
  name: string;
  email?: string;
  phone?: string;
  notes?: string;
  isActive: boolean;
};

const emptyForm = { name: "", email: "", phone: "", notes: "" };
const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function SuppliersPage() {
  const [items, setItems] = useState<Supplier[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function load(inactive = includeInactive) {
    const data = await api<Paged<Supplier>>(`/api/v1/suppliers?pageSize=100&includeInactive=${inactive}`);
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  function startEdit(s: Supplier) {
    setEditingId(s.id);
    setForm({
      name: s.name,
      email: s.email ?? "",
      phone: s.phone ?? "",
      notes: s.notes ?? "",
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
        email: form.email || null,
        phone: form.phone || null,
        notes: form.notes || null,
      };
      if (editingId) {
        await api(`/api/v1/suppliers/${editingId}`, { method: "PATCH", body: JSON.stringify(body) });
      } else {
        await api("/api/v1/suppliers", { method: "POST", body: JSON.stringify(body) });
      }
      cancelEdit();
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(s: Supplier) {
    setError(null);
    try {
      await api(`/api/v1/suppliers/${s.id}/${s.isActive ? "deactivate" : "activate"}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Suppliers</h1>
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
          <label className={labelClass}>Name</label>
          <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Supplier name" required className={fieldClass} />
        </div>
        <div>
          <label className={labelClass}>Email</label>
          <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="Email" className={fieldClass} />
        </div>
        <div>
          <label className={labelClass}>Phone</label>
          <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} placeholder="Phone" className={fieldClass} />
        </div>
        <div>
          <label className={labelClass}>Notes</label>
          <input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} placeholder="Optional" className={fieldClass} />
        </div>
        <div className="flex items-end gap-2">
          <button className="flex-1 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">{editingId ? "Save" : "Add supplier"}</button>
          {editingId && <button type="button" onClick={cancelEdit} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>}
        </div>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Phone</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((s) => (
              <tr key={s.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-medium">{s.name}</td>
                <td className="px-4 py-3">{s.email ?? "—"}</td>
                <td className="px-4 py-3">{s.phone ?? "—"}</td>
                <td className="px-4 py-3">{s.isActive ? "Active" : "Inactive"}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button type="button" onClick={() => startEdit(s)} className="text-xs font-medium underline">Edit</button>
                    <button type="button" onClick={() => void toggleActive(s)} className="text-xs font-medium underline">
                      {s.isActive ? "Deactivate" : "Activate"}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">No suppliers yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
