const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export type ApiError = { error: { code: string; message: string } };

export class ApiClientError extends Error {
  code: string;
  status: number;

  constructor(code: string, message: string, status: number) {
    super(message);
    this.code = code;
    this.status = status;
  }
}

export async function api<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  // Prefer same-origin via Next rewrite so auth cookies stay first-party.
  const base = typeof window === "undefined" ? API_URL : "";
  const res = await fetch(`${base}${path}`, {
    ...options,
    headers,
    credentials: "include",
  });

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      throw new ApiClientError(
        "invalid_response",
        text.slice(0, 300) || res.statusText,
        res.status
      );
    }
  }

  if (!res.ok) {
    const err = data as ApiError | null;
    throw new ApiClientError(
      err?.error?.code ?? "request_failed",
      err?.error?.message ?? res.statusText,
      res.status
    );
  }

  return data as T;
}

export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type Me = {
  id: string;
  email: string;
  displayName: string;
  organizationId: string;
  branchId?: string | null;
  roles: string[];
  permissions: string[];
};
