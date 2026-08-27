"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, Paged } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { useEffect, useState } from "react";

type JournalList = {
  id: string;
  entryNumber: string;
  periodName: string;
  entryDate: string;
  postedAt: string;
  memo?: string | null;
  status: string;
  sourceType: string;
  sourceId: string;
};

export default function JournalsPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [items, setItems] = useState<JournalList[]>([]);
  const [sourceType, setSourceType] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load(st = sourceType) {
    const qs = [
      "pageSize=50",
      st ? `sourceType=${encodeURIComponent(st)}` : "",
    ]
      .filter(Boolean)
      .join("&");
    const data = await api<Paged<JournalList>>(`/api/v1/journals?${qs}`);
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Journals</h1>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-600">Source type</label>
          <input
            value={sourceType}
            onChange={(e) => setSourceType(e.target.value)}
            onBlur={() => load().catch((err) => setError(err.message))}
            placeholder="e.g. SaleCompleted"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
        </div>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Entry #</th>
              <th className="px-4 py-3">Period</th>
              <th className="px-4 py-3">Entry date</th>
              <th className="px-4 py-3">Source</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Memo</th>
            </tr>
          </thead>
          <tbody>
            {items.map((j) => (
              <tr key={j.id} className="border-b border-slate-100">
                <td className="px-4 py-3">
                  <Link
                    href={`/accounting/journals/${j.id}`}
                    className="font-medium text-slate-900 underline"
                  >
                    {j.entryNumber}
                  </Link>
                </td>
                <td className="px-4 py-3">{j.periodName}</td>
                <td className="px-4 py-3 font-mono text-xs">
                  {formatOrgDateTime(j.entryDate, tz)}
                </td>
                <td className="px-4 py-3">{j.sourceType}</td>
                <td className="px-4 py-3">{j.status}</td>
                <td className="px-4 py-3 text-slate-600">{j.memo ?? "—"}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-500">
                  No journal entries.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
