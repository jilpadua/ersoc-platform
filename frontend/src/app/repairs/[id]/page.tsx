"use client";

import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";

type RepairStatus = { code: string; name: string };

type RepairDetail = {
  id: string;
  repairNumber: string;
  statusCode: string;
  statusName: string;
  reportedProblem: string;
  diagnosis?: string;
  estimateAmount?: number;
  totalAmount: number;
  approvalStatus: string;
  statusHistory: {
    changedAt: string;
    reason?: string;
    previousStatusName?: string | null;
    newStatusName: string;
  }[];
  notes: { id: string; body: string; createdAt: string }[];
  serviceLines: { serviceName: string; lineTotal: number }[];
  allowedNextStatuses: RepairStatus[];
};

export default function RepairDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [repair, setRepair] = useState<RepairDetail | null>(null);
  const [nextStatus, setNextStatus] = useState("");
  const [reason, setReason] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const r = await api<RepairDetail>(`/api/v1/repairs/${id}`);
    setRepair(r);
    setNextStatus(r.allowedNextStatuses[0]?.code ?? "");
  }

  useEffect(() => {
    if (!id) return;
    load().catch((e) => setError(e instanceof ApiClientError ? e.message : e.message));
  }, [id]);

  async function changeStatus(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api(`/api/v1/repairs/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ statusCode: nextStatus, reason }),
      });
      setReason("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function addNote(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api(`/api/v1/repairs/${id}/notes`, {
        method: "POST",
        body: JSON.stringify({ body: note }),
      });
      setNote("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  if (!repair) {
    return <AppShell><p className="text-sm text-slate-500">Loading…</p></AppShell>;
  }

  const canAdvance = repair.allowedNextStatuses.length > 0;

  return (
    <AppShell>
      <div className="mb-2 flex flex-wrap items-center gap-3">
        <h1 className="font-mono text-2xl font-semibold">{repair.repairNumber}</h1>
        <span className="rounded bg-slate-900 px-2 py-1 text-xs font-medium text-white">
          {repair.statusName}
        </span>
      </div>
      <p className="text-sm text-slate-700">{repair.reportedProblem}</p>
      <p className="mt-1 text-sm text-slate-500">
        Approval: {repair.approvalStatus} · Total ₱{repair.totalAmount.toLocaleString()}
        {repair.estimateAmount != null ? ` · Estimate ₱${repair.estimateAmount}` : ""}
      </p>

      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}

      <form onSubmit={changeStatus} className="mt-6 flex flex-wrap gap-2 rounded-lg border border-slate-200 bg-white p-4">
        {canAdvance ? (
          <>
            <select required value={nextStatus} onChange={(e) => setNextStatus(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">
              {repair.allowedNextStatuses.map((s) => (
                <option key={s.code} value={s.code}>{s.name}</option>
              ))}
            </select>
            <input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Reason (optional)" className="min-w-[200px] flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm" />
            <button className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">Update status</button>
            <p className="w-full text-xs text-slate-500">
              Only the next allowed workflow steps are listed (e.g. Received → Diagnosis or Cancelled).
            </p>
          </>
        ) : (
          <p className="text-sm text-slate-600">This repair is in a terminal status. No further transitions are allowed.</p>
        )}
      </form>

      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Status history</h2>
          <ul className="space-y-2 text-sm">
            {repair.statusHistory.map((h, i) => (
              <li key={i} className="border-b border-slate-100 pb-2">
                <div className="font-mono text-xs text-slate-500">{new Date(h.changedAt).toLocaleString()}</div>
                <div className="font-medium">
                  {h.previousStatusName ?? "—"} → {h.newStatusName || "Unknown"}
                </div>
                {h.reason && <div className="text-slate-600">{h.reason}</div>}
              </li>
            ))}
          </ul>
        </section>
        <section className="rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Notes</h2>
          <form onSubmit={addNote} className="mb-3 flex gap-2">
            <input value={note} onChange={(e) => setNote(e.target.value)} required placeholder="Add note" className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm" />
            <button className="rounded-md border border-slate-300 px-3 py-2 text-sm">Add</button>
          </form>
          <ul className="space-y-2 text-sm">
            {repair.notes.map((n) => (
              <li key={n.id} className="border-b border-slate-100 pb-2">
                <div className="font-mono text-xs text-slate-500">{new Date(n.createdAt).toLocaleString()}</div>
                <div>{n.body}</div>
              </li>
            ))}
            {repair.notes.length === 0 && <li className="text-slate-500">No notes.</li>}
          </ul>
        </section>
      </div>

      {repair.serviceLines.length > 0 && (
        <section className="mt-4 rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Services</h2>
          <ul className="text-sm">
            {repair.serviceLines.map((l, i) => (
              <li key={i} className="flex justify-between border-b border-slate-100 py-1">
                <span>{l.serviceName}</span>
                <span className="font-mono">₱{l.lineTotal}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </AppShell>
  );
}
