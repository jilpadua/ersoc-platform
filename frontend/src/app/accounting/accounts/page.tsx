"use client";

import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, ApiClientError } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { FormEvent, useEffect, useState } from "react";

type Account = {
  id: string;
  code: string;
  name: string;
  accountType: string;
  normalBalance: string;
  parentAccountId?: string | null;
  isSystem: boolean;
  isActive: boolean;
  createdAt: string;
};

const emptyForm = {
  code: "",
  name: "",
  accountType: "Asset",
  normalBalance: "Debit",
};

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function AccountsPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [items, setItems] = useState<Account[]>([]);
  const [activeOnly, setActiveOnly] = useState(true);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);

  async function load(active = activeOnly) {
    const qs = active ? "?activeOnly=true" : "";
    const data = await api<Account[]>(`/api/v1/accounts${qs}`);
    setItems(data);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/v1/accounts", {
        method: "POST",
        body: JSON.stringify({
          code: form.code,
          name: form.name,
          accountType: form.accountType,
          normalBalance: form.normalBalance,
        }),
      });
      setForm(emptyForm);
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function toggleActive(a: Account) {
    setError(null);
    try {
      await api(`/api/v1/accounts/${a.id}`, {
        method: "PATCH",
        body: JSON.stringify({ name: a.name, isActive: !a.isActive }),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Accounts</h1>
      <form
        onSubmit={onCreate}
        className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-5"
      >
        <div>
          <label className={labelClass}>Code</label>
          <input
            value={form.code}
            onChange={(e) => setForm({ ...form, code: e.target.value })}
            required
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Name</label>
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            required
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Type</label>
          <select
            value={form.accountType}
            onChange={(e) => setForm({ ...form, accountType: e.target.value })}
            className={fieldClass}
          >
            <option value="Asset">Asset</option>
            <option value="Liability">Liability</option>
            <option value="Equity">Equity</option>
            <option value="Revenue">Revenue</option>
            <option value="CostOfGoodsSold">Cost of goods sold</option>
            <option value="Expense">Expense</option>
          </select>
        </div>
        <div>
          <label className={labelClass}>Normal balance</label>
          <select
            value={form.normalBalance}
            onChange={(e) => setForm({ ...form, normalBalance: e.target.value })}
            className={fieldClass}
          >
            <option value="Debit">Debit</option>
            <option value="Credit">Credit</option>
          </select>
        </div>
        <div className="flex items-end">
          <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            Create account
          </button>
        </div>
      </form>
      <label className="mb-3 flex items-center gap-2 text-sm text-slate-600">
        <input
          type="checkbox"
          checked={activeOnly}
          onChange={(e) => {
            const v = e.target.checked;
            setActiveOnly(v);
            load(v).catch((err) => setError(err.message));
          }}
        />
        Active only
      </label>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Code</th>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Type</th>
              <th className="px-4 py-3">Balance</th>
              <th className="px-4 py-3">Created</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.map((a) => (
              <tr key={a.id} className="border-b border-slate-100">
                <td className="px-4 py-3 font-mono text-xs">{a.code}</td>
                <td className="px-4 py-3 font-medium">{a.name}</td>
                <td className="px-4 py-3">{a.accountType}</td>
                <td className="px-4 py-3">{a.normalBalance}</td>
                <td className="px-4 py-3 font-mono text-xs">
                  {formatOrgDateTime(a.createdAt, tz)}
                </td>
                <td className="px-4 py-3">{a.isActive ? "Active" : "Inactive"}</td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => void toggleActive(a)}
                    className="text-xs font-medium text-slate-600 underline"
                  >
                    {a.isActive ? "Deactivate" : "Activate"}
                  </button>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-500">
                  No accounts.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
