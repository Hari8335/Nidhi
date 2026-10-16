import Image from "next/image";
import Link from "next/link";

/**
 * Nidhi Brand logo component.
 * Uses the transparent dark-theme derivative of the approved artwork,
 * ensuring seamless integration into dark charcoal and navy surfaces
 * without any white rectangle container.
 */
export function Brand({ className = "" }: { className?: string }) {
  return (
    <Link href="/" className={`brand ${className}`.trim()} aria-label="Nidhi home">
      <Image
        src="/brand/nidhi-logo-dark.png"
        alt="Nidhi"
        width={2172}
        height={724}
        sizes="(max-width: 639px) 136px, 180px"
        className="brand-image"
      />
    </Link>
  );
}
