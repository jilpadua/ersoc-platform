"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useState } from "react";

type Check = {
  code: string;
  status: string;
  message: string;
  expected?: number | null;
  actual?: number | null;
};

const fieldClass = "rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

function todayDateInput() {
  return new Date().toISOString().slice(0, 10);
}

function toIsoEnd(date: string) {
  return new Date(`${date}T23:59:59.999`).toISOString();
}

export default function ReconciliationPage() {
  const [asOf, setAsOf] = useState(todayDateInput);
  const [items, setItems] = useState<Check[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onRun(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const data = await api<Check[]>(
        `/api/v1/accounting/reports/reconciliation?asOf=${encodeURIComponent(toIsoEnd(asOf))}`
      );
      setItems(data);
    } catch (err: unknown) {
      setItems([]);
      setError(err instanceof ApiClientError ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Reconciliation</h1>
      <form onSubmit={onRun} className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className={labelClass}>As of</label>
          <input
            type="date"
            value={asOf}
            onChange={(e) => setAsOf(e.target.value)}
            required
            className={fieldClass}
          />
        </div>
        <button
          disabled={loading}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {loading ? "Running…" : "Run reconciliation"}
        </button>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Code</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Message</th>
              <th className="px-4 py-3 text-right">Expected</th>
              <th className="px-4 py-3 text-right">Actual</th>
            </tr>
          </thead>
          <tbody>
            {items.map((c) => (
              <tr key={c.code} className="border-b border-slate-100">
                <td className="px-4 py-3 font-medium">{c.code}</td>
                <td className="px-4 py-3">{c.status}</td>
                <td className="px-4 py-3 text-slate-600">{c.message}</td>
                <td className="px-4 py-3 text-right font-mono">
                  {c.expected != null ? `₱${c.expected.toLocaleString()}` : "—"}
                </td>
                <td className="px-4 py-3 text-right font-mono">
                  {c.actual != null ? `₱${c.actual.toLocaleString()}` : "—"}
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-500">
                  Run reconciliation to see results.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
