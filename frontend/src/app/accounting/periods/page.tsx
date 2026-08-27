"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Period = {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
};

const fieldClass = "rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function PeriodsPage() {
  const [items, setItems] = useState<Period[]>([]);
  const [year, setYear] = useState(String(new Date().getFullYear()));
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const data = await api<Period[]>("/api/v1/accounting/periods");
    setItems(data);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onGenerate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/v1/accounting/periods/generate", {
        method: "POST",
        body: JSON.stringify({ year: Number(year) }),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function closeOrReopen(p: Period) {
    setError(null);
    const action = p.status === "Open" ? "close" : "reopen";
    try {
      await api(`/api/v1/accounting/periods/${p.id}/${action}`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Accounting periods</h1>
      <form onSubmit={onGenerate} className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className={labelClass}>Year</label>
          <input
            type="number"
            min="2000"
            max="2100"
            value={year}
            onChange={(e) => setYear(e.target.value)}
            required
            className={fieldClass}
          />
        </div>
        <button className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">
          Generate year
        </button>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Start</th>
              <th className="px-4 py-3">End</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.map((p) => (
              <tr key={p.id} className="border-b border-slate-100">
                <td className="px-4 py-3 font-medium">{p.name}</td>
                <td className="px-4 py-3 font-mono text-xs">{p.startDate}</td>
                <td className="px-4 py-3 font-mono text-xs">{p.endDate}</td>
                <td className="px-4 py-3">{p.status}</td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => void closeOrReopen(p)}
                    className="text-xs font-medium text-slate-600 underline"
                  >
                    {p.status === "Open" ? "Close" : "Reopen"}
                  </button>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-slate-500">
                  No periods yet. Generate a year to start.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
