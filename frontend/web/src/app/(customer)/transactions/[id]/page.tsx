import { TransactionDetail } from "@/components/transactions/transaction-detail";
export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <TransactionDetail key={id} id={id} />;
}
