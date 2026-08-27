"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

type Part = {
  id: string;
  sku: string;
  name: string;
  quantityOnHand: number;
  reorderLevel: number;
};

type Ledger = {
  id: string;
  quantityDelta: number;
  entryType: string;
  reason?: string;
  createdAt: string;
};

export default function PartDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [part, setPart] = useState<Part | null>(null);
  const [ledger, setLedger] = useState<Ledger[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      api<Part>(`/api/v1/parts/${id}`),
      api<Paged<Ledger>>(`/api/v1/parts/${id}/ledger?pageSize=50`),
    ])
      .then(([p, l]) => {
        setPart(p);
        setLedger(l.items);
      })
      .catch((e) => setError(e instanceof ApiClientError ? e.message : e.message));
  }, [id]);

  return (
    <AppShell>
      <Link href="/parts" className="text-sm text-slate-500 underline">← Parts</Link>
      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
      {!part && !error && <p className="mt-4 text-sm text-slate-500">Loading…</p>}
      {part && (
        <>
          <h1 className="mt-2 text-2xl font-semibold">{part.name}</h1>
          <p className="font-mono text-sm text-slate-600">{part.sku}</p>
          <p className="mt-2 text-sm">
            On hand: <span className="font-mono font-semibold">{part.quantityOnHand}</span>
            {" · "}Reorder: <span className="font-mono">{part.reorderLevel}</span>
          </p>
          <section className="mt-6 rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Stock ledger</h2>
            <ul className="space-y-2 text-sm">
              {ledger.map((e) => (
                <li key={e.id} className="flex justify-between border-b border-slate-100 pb-2">
                  <div>
                    <div className="font-mono text-xs text-slate-500">{new Date(e.createdAt).toLocaleString()}</div>
                    <div>{e.entryType}{e.reason ? ` · ${e.reason}` : ""}</div>
                  </div>
                  <span className={`font-mono ${e.quantityDelta < 0 ? "text-red-600" : "text-emerald-700"}`}>
                    {e.quantityDelta > 0 ? `+${e.quantityDelta}` : e.quantityDelta}
                  </span>
                </li>
              ))}
              {ledger.length === 0 && <li className="text-slate-500">No ledger entries yet.</li>}
            </ul>
          </section>
        </>
      )}
    </AppShell>
  );
}
