"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Customer = {
  id: string;
  name: string;
  email?: string;
  phone?: string;
  city?: string;
};

export default function CustomersPage() {
  const [items, setItems] = useState<Customer[]>([]);
  const [search, setSearch] = useState("");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load(q = search) {
    const data = await api<Paged<Customer>>(
      `/api/v1/customers?page=1&pageSize=50&search=${encodeURIComponent(q)}`
    );
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/v1/customers", {
        method: "POST",
        body: JSON.stringify({ name, phone }),
      });
      setName("");
      setPhone("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex items-end justify-between gap-4">
        <h1 className="text-2xl font-semibold">Customers</h1>
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
            placeholder="Search name or phone"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          <button className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm">
            Filter
          </button>
        </form>
      </div>

      <form
        onSubmit={onCreate}
        className="mb-4 grid gap-2 rounded-lg border border-slate-200 bg-white p-4 sm:grid-cols-3"
      >
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Name"
          required
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <input
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          placeholder="Phone"
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
        <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
          Add customer
        </button>
      </form>

      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Name</th>
              <th className="px-4 py-3">Phone</th>
              <th className="px-4 py-3">Email</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={3} className="px-4 py-8 text-center text-slate-500">
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
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
