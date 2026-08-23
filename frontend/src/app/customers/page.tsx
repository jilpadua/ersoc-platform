"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Customer = {
  id: string;
  name: string;
  email?: string;
  phone?: string;
  notes?: string;
  isActive: boolean;
};

const emptyForm = { name: "", email: "", phone: "", notes: "" };

export default function CustomersPage() {
  const [items, setItems] = useState<Customer[]>([]);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load(q = search, inactive = includeInactive) {
    const data = await api<Paged<Customer>>(
      `/api/v1/customers?page=1&pageSize=50&search=${encodeURIComponent(q)}&includeInactive=${inactive}`
    );
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  function startEdit(c: Customer) {
    setEditingId(c.id);
    setForm({
      name: c.name,
      email: c.email ?? "",
      phone: c.phone ?? "",
      notes: c.notes ?? "",
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
        await api(`/api/v1/customers/${editingId}`, {
          method: "PATCH",
          body: JSON.stringify(body),
        });
      } else {
        await api("/api/v1/customers", {
          method: "POST",
          body: JSON.stringify(body),
        });
      }
      cancelEdit();
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(c: Customer) {
    setError(null);
    try {
      const path = c.isActive
        ? `/api/v1/customers/${c.id}/deactivate`
        : `/api/v1/customers/${c.id}/activate`;
      await api(path, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <h1 className="text-2xl font-semibold">Customers</h1>
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => {
                const v = e.target.checked;
                setIncludeInactive(v);
                load(search, v).catch((err) => setError(err.message));
              }}
            />
            Show inactive
          </label>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              load().catch((err) => setError(err.message));
            }}
            className="flex gap-2"
          >
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search name, phone, email"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm"
            />
            <button className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm">
              Filter
            </button>
          </form>
        </div>
      </div>

      <form
        onSubmit={onSubmit}
        className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-5"
      >
        <input
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
          placeholder="Name"
          required
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <input
          type="email"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          placeholder="Email"
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <input
          value={form.phone}
          onChange={(e) => setForm({ ...form, phone: e.target.value })}
          placeholder="Phone"
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <input
          value={form.notes}
          onChange={(e) => setForm({ ...form, notes: e.target.value })}
          placeholder="Notes"
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <div className="flex gap-2">
          <button className="flex-1 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            {editingId ? "Save" : "Add customer"}
          </button>
          {editingId && (
            <button
              type="button"
              onClick={cancelEdit}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm"
            >
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
              <th className="px-4 py-3">Phone</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-500">
                  No customers yet.
                </td>
              </tr>
            ) : (
              items.map((c) => (
                <tr key={c.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <Link href={`/customers/${c.id}`} className="font-medium text-slate-900 underline">
                      {c.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3 font-mono text-xs">{c.phone ?? "—"}</td>
                  <td className="px-4 py-3">{c.email ?? "—"}</td>
                  <td className="px-4 py-3">
                    <span className={`rounded px-2 py-0.5 text-xs ${c.isActive ? "bg-emerald-50 text-emerald-800" : "bg-slate-100 text-slate-500"}`}>
                      {c.isActive ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2">
                      <button type="button" onClick={() => startEdit(c)} className="text-xs font-medium underline">
                        Edit
                      </button>
                      <button type="button" onClick={() => void toggleActive(c)} className="text-xs font-medium underline">
                        {c.isActive ? "Deactivate" : "Activate"}
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
