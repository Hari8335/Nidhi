import { AdminLedgerPage } from "@/components/admin/admin-pages";
export default async function Page({ searchParams }: { searchParams: Promise<{ transactionId?: string }> }) { const query = await searchParams; return <AdminLedgerPage transactionId={typeof query.transactionId === "string" ? query.transactionId : ""} />; }
