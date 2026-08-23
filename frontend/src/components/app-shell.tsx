"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth-provider";
import { FormEvent, useEffect, useState } from "react";
import { api } from "@/lib/api";

const nav = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/customers", label: "Customers" },
  { href: "/devices", label: "Devices" },
  { href: "/repairs", label: "Repairs" },
  { href: "/services", label: "Services" },
  { href: "/audit", label: "Audit" },
  { href: "/settings", label: "Settings" },
];

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout, loading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const [q, setQ] = useState("");
  const [results, setResults] = useState<
    { type: string; id: string; title: string; subtitle?: string }[]
  >([]);

  useEffect(() => {
    if (!loading && !user) {
      router.replace("/login");
    }
  }, [loading, user, router]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Loading…
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Redirecting…
      </div>
    );
  }

  async function onSearch(e: FormEvent) {
    e.preventDefault();
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    const data = await api<typeof results>(
      `/api/v1/search?q=${encodeURIComponent(q.trim())}`
    );
    setResults(data);
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <div className="mx-auto flex min-h-screen max-w-[1400px]">
        <aside className="flex w-56 flex-col border-r border-slate-200 bg-white px-3 py-5">
          <div className="mb-6 px-2">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              ERSMS
            </div>
            <div className="text-lg font-semibold text-slate-900">
              Repair Ops
            </div>
          </div>
          <nav className="flex flex-1 flex-col gap-1">
            {nav.map((item) => {
              const active = pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`rounded-md px-3 py-2 text-sm font-medium ${
                    active
                      ? "bg-slate-900 text-white"
                      : "text-slate-700 hover:bg-slate-100"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <div className="mt-4 border-t border-slate-200 px-2 pt-4 text-sm">
            <div className="font-medium">{user.displayName}</div>
            <div className="text-xs text-slate-500">{user.email}</div>
            <button
              type="button"
              onClick={() => void logout().then(() => router.push("/login"))}
              className="mt-3 text-xs font-medium text-slate-600 underline"
            >
              Sign out
            </button>
          </div>
        </aside>
        <main className="flex-1 p-6">
          <form onSubmit={onSearch} className="mb-4 flex gap-2">
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Search repairs, customers, devices…"
              className="w-full max-w-xl rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-slate-500"
            />
            <button
              type="submit"
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
            >
              Search
            </button>
          </form>
          {results.length > 0 && (
            <div className="mb-4 rounded-md border border-slate-200 bg-white p-3 text-sm">
              {results.map((r) => (
                <Link
                  key={`${r.type}-${r.id}`}
                  href={
                    r.type === "repair"
                      ? `/repairs/${r.id}`
                      : r.type === "customer"
                        ? `/customers/${r.id}`
                        : `/devices/${r.id}`
                  }
                  className="block rounded px-2 py-1.5 hover:bg-slate-50"
                  onClick={() => setResults([])}
                >
                  <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs uppercase text-slate-600">
                    {r.type}
                  </span>
                  {r.title}
                  {r.subtitle ? (
                    <span className="text-slate-500"> — {r.subtitle}</span>
                  ) : null}
                </Link>
              ))}
            </div>
          )}
          {children}
        </main>
      </div>
    </div>
  );
}
