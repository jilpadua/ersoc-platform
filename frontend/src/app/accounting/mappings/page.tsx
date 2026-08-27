"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Mapping = {
  mappingKey: string;
  accountId: string;
  accountCode: string;
  accountName: string;
};

type Account = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
};

const MAPPING_KEYS = [
  "Cash",
  "Bank",
  "CardClearing",
  "AccountsReceivable",
  "InventoryAsset",
  "AccountsPayable",
  "OpeningEquity",
  "SalesRevenue",
  "Cogs",
  "InventoryAdjustment",
  "OperatingExpense",
];

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function MappingsPage() {
  const [items, setItems] = useState<Mapping[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [mappingKey, setMappingKey] = useState(MAPPING_KEYS[0]);
  const [accountId, setAccountId] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [maps, accts] = await Promise.all([
      api<Mapping[]>("/api/v1/accounting/mappings"),
      api<Account[]>("/api/v1/accounts?activeOnly=true"),
    ]);
    setItems(maps);
    setAccounts(accts);
    if (!accountId && accts[0]) setAccountId(accts[0].id);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onUpsert(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/v1/accounting/mappings", {
        method: "PUT",
        body: JSON.stringify({ mappingKey, accountId }),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Account mappings</h1>
      <form
        onSubmit={onUpsert}
        className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3"
      >
        <div>
          <label className={labelClass}>Mapping key</label>
          <select
            value={mappingKey}
            onChange={(e) => setMappingKey(e.target.value)}
            className={fieldClass}
          >
            {MAPPING_KEYS.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className={labelClass}>Account</label>
          <select
            value={accountId}
            onChange={(e) => setAccountId(e.target.value)}
            required
            className={fieldClass}
          >
            <option value="">Select account</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.code} — {a.name}
              </option>
            ))}
          </select>
        </div>
        <div className="flex items-end">
          <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            Upsert mapping
          </button>
        </div>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Key</th>
              <th className="px-4 py-3">Account</th>
            </tr>
          </thead>
          <tbody>
            {items.map((m) => (
              <tr key={m.mappingKey} className="border-b border-slate-100">
                <td className="px-4 py-3 font-medium">{m.mappingKey}</td>
                <td className="px-4 py-3">
                  <span className="font-mono text-xs">{m.accountCode}</span> {m.accountName}
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={2} className="px-4 py-8 text-center text-slate-500">
                  No mappings.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
