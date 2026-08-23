"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { DEVICE_TYPES } from "@/lib/device-types";
import { FormEvent, useEffect, useState } from "react";

type Device = {
  id: string;
  customerId: string;
  deviceType: string;
  brand: string;
  model: string;
  serialNumber?: string;
  imei?: string;
  isActive: boolean;
};

type Customer = { id: string; name: string };

const emptyForm = {
  customerId: "",
  deviceType: "",
  brand: "",
  model: "",
  serialNumber: "",
  imei: "",
};

export default function DevicesPage() {
  const [items, setItems] = useState<Device[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const deviceTypeOptions =
    form.deviceType && !(DEVICE_TYPES as readonly string[]).includes(form.deviceType)
      ? [form.deviceType, ...DEVICE_TYPES]
      : [...DEVICE_TYPES];

  async function load(inactive = includeInactive) {
    const [devices, cust] = await Promise.all([
      api<Paged<Device>>(`/api/v1/devices?pageSize=50&includeInactive=${inactive}`),
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

  function startEdit(d: Device) {
    setEditingId(d.id);
    setForm({
      customerId: d.customerId,
      deviceType: d.deviceType,
      brand: d.brand,
      model: d.model,
      serialNumber: d.serialNumber ?? "",
      imei: d.imei ?? "",
    });
  }

  function cancelEdit() {
    setEditingId(null);
    setForm((f) => ({
      ...emptyForm,
      customerId: f.customerId || customers[0]?.id || "",
    }));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const body = {
        customerId: form.customerId,
        deviceType: form.deviceType,
        brand: form.brand,
        model: form.model,
        serialNumber: form.serialNumber || null,
        imei: form.imei || null,
      };
      if (editingId) {
        await api(`/api/v1/devices/${editingId}`, { method: "PATCH", body: JSON.stringify(body) });
      } else {
        await api("/api/v1/devices", { method: "POST", body: JSON.stringify(body) });
      }
      cancelEdit();
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(d: Device) {
    setError(null);
    try {
      await api(`/api/v1/devices/${d.id}/${d.isActive ? "deactivate" : "activate"}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Devices</h1>
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
      <form onSubmit={onSubmit} className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3">
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
        <select
          value={form.deviceType}
          onChange={(e) => setForm({ ...form, deviceType: e.target.value })}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          required
        >
          <option value="">Select device type</option>
          {deviceTypeOptions.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <input value={form.brand} onChange={(e) => setForm({ ...form, brand: e.target.value })} placeholder="Brand" className="rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <input value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} placeholder="Model" className="rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <input value={form.serialNumber} onChange={(e) => setForm({ ...form, serialNumber: e.target.value })} placeholder="Serial" className="rounded-md border border-slate-300 px-3 py-2 text-sm" />
        <div className="flex gap-2">
          <button className="flex-1 rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            {editingId ? "Save" : "Add device"}
          </button>
          {editingId && (
            <button type="button" onClick={cancelEdit} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Cancel</button>
          )}
        </div>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Device</th>
              <th className="px-4 py-3">Type</th>
              <th className="px-4 py-3">Serial / IMEI</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Actions</th>
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
                <td className="px-4 py-3">
                  <span className={`rounded px-2 py-0.5 text-xs ${d.isActive ? "bg-emerald-50 text-emerald-800" : "bg-slate-100 text-slate-500"}`}>
                    {d.isActive ? "Active" : "Inactive"}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button type="button" onClick={() => startEdit(d)} className="text-xs font-medium underline">Edit</button>
                    <button type="button" onClick={() => void toggleActive(d)} className="text-xs font-medium underline">
                      {d.isActive ? "Deactivate" : "Activate"}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-8 text-center text-slate-500">No devices yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
