"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { useEffect, useState } from "react";

type JournalLine = {
  id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  description?: string | null;
};

type JournalDetail = {
  id: string;
  entryNumber: string;
  periodName: string;
  entryDate: string;
  postedAt: string;
  memo?: string | null;
  status: string;
  sourceType: string;
  sourceId: string;
  reversesJournalEntryId?: string | null;
  lines: JournalLine[];
};

export default function JournalDetailPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [entry, setEntry] = useState<JournalDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<JournalDetail>(`/api/v1/journals/${params.id}`)
      .then(setEntry)
      .catch((e) => setError(e.message));
  }, [params.id]);

  const totalDebit = entry?.lines.reduce((s, l) => s + l.debit, 0) ?? 0;
  const totalCredit = entry?.lines.reduce((s, l) => s + l.credit, 0) ?? 0;

  return (
    <AppShell>
      <div className="mb-4">
        <Link href="/accounting/journals" className="text-sm text-slate-600 underline">
          ← Journals
        </Link>
        <h1 className="mt-2 text-2xl font-semibold">
          {entry ? entry.entryNumber : "Journal entry"}
        </h1>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      {entry && (
        <>
          <div className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 text-sm md:grid-cols-3">
            <div>
              <div className="text-xs uppercase text-slate-500">Period</div>
              <div>{entry.periodName}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Entry date</div>
              <div className="font-mono text-xs">{formatOrgDateTime(entry.entryDate, tz)}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Posted</div>
              <div className="font-mono text-xs">{formatOrgDateTime(entry.postedAt, tz)}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Source</div>
              <div>
                {entry.sourceType}{" "}
                <span className="font-mono text-xs text-slate-500">{entry.sourceId}</span>
              </div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Status</div>
              <div>{entry.status}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Memo</div>
              <div>{entry.memo ?? "—"}</div>
            </div>
          </div>
          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3">Account</th>
                  <th className="px-4 py-3">Description</th>
                  <th className="px-4 py-3 text-right">Debit</th>
                  <th className="px-4 py-3 text-right">Credit</th>
                </tr>
              </thead>
              <tbody>
                {entry.lines.map((l) => (
                  <tr key={l.id} className="border-b border-slate-100">
                    <td className="px-4 py-3">
                      <span className="font-mono text-xs">{l.accountCode}</span> {l.accountName}
                    </td>
                    <td className="px-4 py-3 text-slate-600">{l.description ?? "—"}</td>
                    <td className="px-4 py-3 text-right font-mono">
                      {l.debit ? `₱${l.debit.toLocaleString()}` : ""}
                    </td>
                    <td className="px-4 py-3 text-right font-mono">
                      {l.credit ? `₱${l.credit.toLocaleString()}` : ""}
                    </td>
                  </tr>
                ))}
                <tr className="bg-slate-50 font-medium">
                  <td className="px-4 py-3" colSpan={2}>
                    Totals
                  </td>
                  <td className="px-4 py-3 text-right font-mono">
                    ₱{totalDebit.toLocaleString()}
                  </td>
                  <td className="px-4 py-3 text-right font-mono">
                    ₱{totalCredit.toLocaleString()}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </>
      )}
    </AppShell>
  );
}
