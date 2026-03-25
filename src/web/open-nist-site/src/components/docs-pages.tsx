import { ArrowRight, BookOpenText, Boxes, Fingerprint, FileCode2, ScanSearch } from "lucide-react";
import { Link } from "@tanstack/react-router";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function LibraryDocumentationPage() {
  return (
    <section className="px-6 py-18 md:px-10 md:py-22">
      <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-10">
        <div className="max-w-3xl space-y-4">
          <Badge className="rounded-full border-0 bg-[var(--color-secondary-container)] px-4 py-1.5 text-[0.72rem] uppercase tracking-[0.22em] text-[var(--color-secondary)]">
            Placeholder documentation
          </Badge>
          <h1 className="font-display text-5xl font-semibold tracking-[-0.06em] text-[var(--color-primary)]">
            Library documentation
          </h1>
          <p className="text-lg leading-8 text-[var(--color-on-surface-variant)]">
            This page is the future home for practical OpenNist usage guides: working with image codecs, reading NIST
            structures, and running NFIQ 2 from .NET and WebAssembly.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
          <DocCard
            icon={BookOpenText}
            title="Getting Started"
            description="Package installation, first decode call, and basic file loading examples."
          />
          <DocCard
            icon={FileCode2}
            title="Encode and Decode"
            description="How the combined image-codec workflow maps onto the library surface."
          />
          <DocCard
            icon={Boxes}
            title="NIST Structures"
            description="Record access, field layout, and future editing guidance."
          />
          <DocCard
            icon={ScanSearch}
            title="NFIQ 2"
            description="Managed scoring, measure interpretation, and browser/runtime usage."
          />
        </div>

        <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
          <CardHeader>
            <CardTitle className="font-display text-2xl tracking-[-0.04em] text-[var(--color-primary)]">
              Planned documentation structure
            </CardTitle>
            <CardDescription className="text-[var(--color-on-surface-variant)]">
              The point of this placeholder is to reserve the information architecture now rather than bolt it on later.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2">
            <InfoRow label="Core library usage" value="NuGet install, file loading, image conversion" />
            <InfoRow label="Browser integration" value="WASM runtime loading and app-shell flows" />
            <InfoRow label="Biometric standards" value="NIST package handling, WSQ, JPEG2000" />
            <InfoRow label="Quality scoring" value="NFIQ 2 input expectations and result interpretation" />
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

export function BiometricSubjectsPage() {
  return (
    <section className="px-6 py-18 md:px-10 md:py-22">
      <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-10">
        <div className="max-w-3xl space-y-4">
          <Badge className="rounded-full border-0 bg-[var(--color-primary)] px-4 py-1.5 text-[0.72rem] uppercase tracking-[0.22em] text-[var(--on-primary)]">
            Subject reference
          </Badge>
          <h1 className="font-display text-5xl font-semibold tracking-[-0.06em] text-[var(--color-primary)]">
            Biometric subjects
          </h1>
          <p className="text-lg leading-8 text-[var(--color-on-surface-variant)]">
            This placeholder section is for background documentation on the biometric subjects and standards that
            OpenNist depends on, not just how to call the code.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          <SubjectCard
            icon={Fingerprint}
            title="Fingerprints"
            description="Capture classes, grayscale image expectations, ridge detail, and why resolution matters."
          />
          <SubjectCard
            icon={Boxes}
            title="ANSI/NIST Packages"
            description="Logical records, field numbering, transaction structure, and image-bearing record families."
          />
          <SubjectCard
            icon={ScanSearch}
            title="NFIQ 2"
            description="What the score represents, how quality features are derived, and what the result is useful for."
          />
        </div>

        <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
          <CardHeader>
            <CardTitle className="font-display text-2xl tracking-[-0.04em] text-[var(--color-primary)]">
              Future topic map
            </CardTitle>
            <CardDescription className="text-[var(--color-on-surface-variant)]">
              This page should eventually work like a subject handbook alongside the API docs.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <InfoRow label="WSQ" value="Compression model, grayscale assumptions, operational tradeoffs" />
            <InfoRow label="JPEG2000" value="Codestream structure, lossy/lossless use, transport considerations" />
            <InfoRow label="NIST records" value="Type families, field semantics, interchange context" />
            <InfoRow label="Capture quality" value="How scoring and downstream matching depend on acquisition quality" />
          </CardContent>
        </Card>

        <div>
          <Button
            asChild
            className="rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
          >
            <Link to="/app/nfiq">
              <ArrowRight className="size-4" />
              Open NFIQ workspace
            </Link>
          </Button>
        </div>
      </div>
    </section>
  );
}

function DocCard({
  icon: Icon,
  title,
  description,
}: {
  icon: typeof BookOpenText;
  title: string;
  description: string;
}) {
  return (
    <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
      <CardHeader className="space-y-4">
        <div className="flex size-12 items-center justify-center rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] text-[var(--color-primary)]">
          <Icon className="size-5" />
        </div>
        <div>
          <CardTitle className="font-display text-xl tracking-[-0.03em] text-[var(--color-primary)]">{title}</CardTitle>
          <CardDescription className="mt-2 leading-7 text-[var(--color-on-surface-variant)]">
            {description}
          </CardDescription>
        </div>
      </CardHeader>
    </Card>
  );
}

function SubjectCard({
  icon: Icon,
  title,
  description,
}: {
  icon: typeof Fingerprint;
  title: string;
  description: string;
}) {
  return (
    <Card className="surface-module border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]">
      <CardHeader className="space-y-4">
        <div className="flex size-12 items-center justify-center rounded-[var(--radius-lg)] bg-[var(--color-primary-container)] text-[var(--color-on-primary-container)]">
          <Icon className="size-5" />
        </div>
        <div>
          <CardTitle className="font-display text-xl tracking-[-0.03em] text-[var(--color-primary)]">{title}</CardTitle>
          <CardDescription className="mt-2 leading-7 text-[var(--color-on-surface-variant)]">
            {description}
          </CardDescription>
        </div>
      </CardHeader>
    </Card>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-low)] px-4 py-3">
      <p className="font-mono text-[0.64rem] uppercase tracking-[0.18em] text-[var(--color-on-surface-variant)]">{label}</p>
      <p className="mt-2 text-sm leading-7 text-[var(--color-on-surface)]">{value}</p>
    </div>
  );
}
