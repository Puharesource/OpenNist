import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import {
  AppWindow,
  ArrowRight,
  BookOpenText,
  CheckCircle2,
  Code2,
  FileImage,
  FolderTree,
  Fingerprint,
  Package,
  PanelLeft,
  Sparkles,
} from "lucide-react";
import { Link } from "@tanstack/react-router";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import {
  codeSampleLines,
  landingCapabilities,
  landingFeatures,
  landingStats,
  runtimePoints,
} from "@/lib/site-content";

export function LandingPage() {
  return (
    <>
      <HeroSection />
      <StatsSection />
      <CapabilitiesSection />
      <EngineeringSection />
      <RuntimeSection />
      <InstallSection />
    </>
  );
}

function HeroSection() {
  return (
    <section className="relative overflow-hidden px-6 pb-18 pt-18 md:px-10 md:pb-24 md:pt-24">
      <div className="mx-auto grid w-full max-w-[1440px] gap-14 lg:grid-cols-[minmax(0,1.1fr)_minmax(420px,0.9fr)] lg:items-center">
        <div className="space-y-8">
          <Badge className="rounded-full border-0 bg-[var(--color-secondary-container)] px-4 py-1.5 text-[0.72rem] uppercase tracking-[0.22em] text-[var(--color-secondary)]">
            Managed NFIQ 2 runtime available
          </Badge>

          <div className="space-y-5">
            <h1 className="max-w-5xl font-display text-5xl font-semibold leading-[0.95] tracking-[-0.07em] text-[var(--color-primary)] md:text-7xl">
              Native NIST, WSQ &amp; JPEG2000 for modern .NET
            </h1>
            <p className="max-w-2xl text-lg leading-8 text-[var(--color-on-surface-variant)] md:text-xl">
              OpenNist is a standards-focused biometric imaging toolkit for .NET, with managed WSQ, NIST, JPEG2000
              and NFIQ workflows that also target WebAssembly.
            </p>
          </div>

          <div className="flex flex-wrap gap-4">
            <Button
              asChild
              size="lg"
              className="h-12 rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] px-6 text-[var(--on-primary)] hover:opacity-95"
            >
              <Link to="/app/codecs">
                <AppWindow className="size-4" />
                Open Engineering App
              </Link>
            </Button>
            <Button
              asChild
              variant="outline"
              size="lg"
              className="h-12 rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-lowest)] px-6 text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
            >
              <a href="#install">
                <Package className="size-4" />
                Install Package
              </a>
            </Button>
          </div>
        </div>

        <Card className="surface-module ghost-outline overflow-hidden border-0 bg-[var(--color-surface-container-lowest)] shadow-[var(--effect-modal-shadow)]">
          <CardHeader className="border-b border-[color:var(--effect-ghost-border)] pb-4">
            <div className="flex items-center justify-between gap-4">
              <div>
                <CardTitle className="font-display text-xl tracking-[-0.04em] text-[var(--color-primary)]">
                  OpenNist Engineering
                </CardTitle>
                <CardDescription className="mt-1 max-w-md text-[var(--color-on-surface-variant)]">
                  Landing page and engineering workspace in one browser surface, with shared runtime language and shared
                  design tokens.
                </CardDescription>
              </div>
              <Badge className="rounded-full border-0 bg-[var(--color-surface-container-high)] px-3 py-1 text-[var(--color-primary)]">
                /app
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="grid gap-6 p-6">
            <div className="rounded-[var(--radius-xl)] bg-[var(--color-surface-container-high)] p-5">
              <div className="flex items-center justify-between gap-3 border-b border-[color:var(--effect-ghost-border)] pb-4">
                <div className="flex items-center gap-3">
                  <div className="flex size-11 items-center justify-center rounded-[var(--radius-md)] bg-[var(--color-primary)] text-[var(--on-primary)]">
                    <PanelLeft className="size-5" />
                  </div>
                  <div>
                    <p className="font-display text-lg tracking-[-0.03em] text-[var(--color-primary)]">
                      Sidebar-driven workspace
                    </p>
                    <p className="text-sm text-[var(--color-on-surface-variant)]">
                      Codec, NIST, and NFIQ views with a right-hand inspector
                    </p>
                  </div>
                </div>
                <ArrowRight className="size-5 text-[var(--color-primary)]" />
              </div>

              <div className="mt-5 grid gap-3">
                <RuntimeFlowRow icon={FileImage} label="Image Codecs" value="WSQ, JPEG2000, and raster conversion" />
                <RuntimeFlowRow icon={FolderTree} label="NIST Structure" value="Static placeholder inspector for now" />
                <RuntimeFlowRow icon={Fingerprint} label="NFIQ 2 Review" value="Managed scoring workspace shell" />
              </div>
            </div>

            <div className="rounded-[var(--radius-xl)] bg-[var(--color-primary)] p-5 text-[var(--on-primary)]">
              <div className="mb-4 flex items-center justify-between gap-3">
                <p className="font-display text-lg tracking-[-0.03em]">Example API</p>
                <Code2 className="size-5 text-[var(--color-primary-fixed)]" />
              </div>
              <pre className="overflow-x-auto font-mono text-sm leading-7 text-[var(--color-primary-fixed)]">
                <code>{`using OpenNist;

byte[] bytes = await File.ReadAllBytesAsync("sample.nist");
var file = NistDecoder.Decode(bytes);
var image = file.Records[0].AsFingerprintImage();
var score = Nfiq2Algorithm.Default.Assess(image);`}</code>
              </pre>
            </div>
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

function StatsSection() {
  return (
    <section className="border-y border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] px-6 py-10 md:px-10">
      <div className="mx-auto grid w-full max-w-[1440px] gap-8 md:grid-cols-3">
        {landingStats.map((stat) => (
          <div key={stat.label} className="space-y-2">
            <p className="font-display text-4xl font-semibold tracking-[-0.05em] text-[var(--color-primary)]">
              {stat.value}
            </p>
            <p className="font-mono text-[0.74rem] uppercase tracking-[0.2em] text-[var(--color-on-surface-variant)]">
              {stat.label}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}

function CapabilitiesSection() {
  return (
    <section id="capabilities" className="px-6 py-20 md:px-10 md:py-24">
      <div className="mx-auto grid w-full max-w-[1440px] gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(420px,0.92fr)] lg:items-start">
        <div className="space-y-8">
          <div className="max-w-2xl space-y-4">
            <p className="font-mono text-[0.74rem] uppercase tracking-[0.24em] text-[var(--color-secondary)]">
              Powerful Biometric Conversion
            </p>
            <h2 className="font-display text-4xl font-semibold tracking-[-0.05em] text-[var(--color-primary)] md:text-5xl">
              Format workflows built for operational biometric systems
            </h2>
            <p className="text-lg leading-8 text-[var(--color-on-surface-variant)]">
              The project is centered on the formats and record types that actually show up in biometric pipelines,
              not generic image editing abstractions.
            </p>
          </div>

          <div className="grid gap-6">
            {landingCapabilities.map((capability) => (
              <div key={capability.title} className="flex gap-5 rounded-[var(--radius-xl)] bg-[var(--color-surface-container-low)] p-5">
                <div className="flex size-12 shrink-0 items-center justify-center rounded-full bg-[var(--color-primary)]/6 text-[var(--color-primary)]">
                  <capability.icon className="size-5" />
                </div>
                <div className="space-y-2">
                  <h3 className="font-display text-xl font-semibold tracking-[-0.03em] text-[var(--color-primary)]">
                    {capability.title}
                  </h3>
                  <p className="leading-7 text-[var(--color-on-surface-variant)]">{capability.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        <Card className="surface-module ghost-outline border-0 shadow-[var(--effect-modal-shadow)]">
          <CardHeader className="pb-4">
            <CardTitle className="font-display text-2xl tracking-[-0.04em] text-[var(--color-primary)]">
              Conversion Matrix
            </CardTitle>
            <CardDescription className="text-[var(--color-on-surface-variant)]">
              A simplified view of how OpenNist fits into typical package and image flows.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] p-4">
              <PipelineRow source="NIST Type-4 / Type-14" target="Managed object graph" />
              <Separator className="my-4 bg-[color:var(--effect-ghost-border)]" />
              <PipelineRow source="Raw grayscale raster" target="WSQ or JPEG2000" />
              <Separator className="my-4 bg-[color:var(--effect-ghost-border)]" />
              <PipelineRow source="Fingerprint image" target="Managed NFIQ 2 result" />
            </div>

            <div className="grid grid-cols-3 gap-3">
              <FormatChip>WSQ</FormatChip>
              <FormatChip>JP2</FormatChip>
              <FormatChip>NIST</FormatChip>
              <FormatChip>NFIQ</FormatChip>
              <FormatChip>WASM</FormatChip>
              <FormatChip>.NET</FormatChip>
            </div>
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

function EngineeringSection() {
  return (
    <section id="engineering" className="bg-[var(--color-surface-container-low)] px-6 py-20 md:px-10 md:py-24">
      <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-12">
        <div className="flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
          <div className="max-w-2xl space-y-4">
            <p className="font-mono text-[0.74rem] uppercase tracking-[0.24em] text-[var(--color-secondary)]">
              Engineering Excellence
            </p>
            <h2 className="font-display text-4xl font-semibold tracking-[-0.05em] text-[var(--color-primary)] md:text-5xl">
              A managed API for biometric image standards
            </h2>
            <p className="text-lg leading-8 text-[var(--color-on-surface-variant)]">
              The library keeps the operational surface direct and explicit: records, codecs, metrics, and exports
              instead of layers of image-processing indirection.
            </p>
          </div>
          <div className="h-1.5 w-28 rounded-full bg-[var(--color-primary)]" />
        </div>

        <div className="grid gap-6 md:grid-cols-3">
          {landingFeatures.map((feature) => (
            <Card
              key={feature.title}
              className="surface-module pixel-grid-overlay border-0 shadow-none ring-1 ring-[color:var(--effect-ghost-border)]"
            >
              <CardHeader className="space-y-5">
                <div className={`flex size-13 items-center justify-center rounded-[var(--radius-lg)] ${feature.tone}`}>
                  <feature.icon className="size-6" />
                </div>
                <div className="space-y-2">
                  <CardTitle className="font-display text-xl tracking-[-0.03em] text-[var(--color-primary)]">
                    {feature.title}
                  </CardTitle>
                  <CardDescription className="leading-7 text-[var(--color-on-surface-variant)]">
                    {feature.description}
                  </CardDescription>
                </div>
              </CardHeader>
            </Card>
          ))}
        </div>
      </div>
    </section>
  );
}

function RuntimeSection() {
  return (
    <section id="runtime" className="bg-[var(--color-primary)] px-6 py-20 text-[var(--on-primary)] md:px-10 md:py-24">
      <div className="mx-auto grid w-full max-w-[1440px] gap-14 lg:grid-cols-[minmax(0,0.95fr)_minmax(480px,1.05fr)] lg:items-center">
        <div className="space-y-7">
          <div className="space-y-4">
            <p className="font-mono text-[0.74rem] uppercase tracking-[0.24em] text-[var(--color-primary-fixed)]">
              Technical Implementation
            </p>
            <h2 className="font-display text-4xl font-semibold tracking-[-0.05em] md:text-5xl">
              Managed internals, standards-facing API
            </h2>
            <p className="text-lg leading-8 text-[var(--color-primary-fixed)]">
              OpenNist hides the messy parts of biometric packaging and image interchange while keeping the important
              structures accessible to application code.
            </p>
          </div>

          <div className="grid gap-3">
            {runtimePoints.map((point) => (
              <div key={point} className="flex items-center gap-3 text-[var(--color-primary-fixed)]">
                <CheckCircle2 className="size-5 text-[var(--color-secondary-fixed)]" />
                <span>{point}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="relative">
          <div className="absolute inset-0 rounded-[calc(var(--radius-xl)+10px)] bg-[var(--color-secondary)]/10 blur-3xl" />
          <div className="relative overflow-hidden rounded-[calc(var(--radius-xl)+6px)] border border-white/10 bg-[#081120] shadow-[var(--effect-modal-shadow)]">
            <div className="flex items-center gap-2 border-b border-white/10 bg-black/20 px-4 py-3">
              <div className="size-3 rounded-full bg-red-400/70" />
              <div className="size-3 rounded-full bg-amber-300/70" />
              <div className="size-3 rounded-full bg-emerald-400/70" />
              <span className="ml-3 font-mono text-xs uppercase tracking-[0.24em] text-white/45">Nfiq2Algorithm.cs</span>
            </div>
            <div className="overflow-x-auto px-6 py-6 font-mono text-sm leading-7 text-[var(--color-primary-fixed)]">
              <div className="grid grid-cols-[auto_1fr] gap-x-4">
                {codeSampleLines.map((line, index) => (
                  <CodeLine key={line} number={index + 1} line={line} />
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function InstallSection() {
  return (
    <section id="install" className="px-6 py-20 md:px-10 md:py-24">
      <div className="mx-auto flex w-full max-w-[980px] flex-col items-center gap-8 text-center">
        <div className="flex size-22 items-center justify-center rounded-full bg-[var(--color-primary-container)] text-[var(--color-on-primary-container)]">
          <Sparkles className="size-9" />
        </div>
        <div className="space-y-4">
          <h2 className="font-display text-4xl font-semibold tracking-[-0.05em] text-[var(--color-primary)] md:text-5xl">
            Ready to integrate?
          </h2>
          <p className="mx-auto max-w-2xl text-lg leading-8 text-[var(--color-on-surface-variant)]">
            Install the package, open the engineering app shell, and start shaping the browser-side workflow for
            codecs, NIST, and NFIQ.
          </p>
        </div>

        <Card className="surface-module ghost-outline w-full max-w-3xl border-0 shadow-[var(--effect-modal-shadow)]">
          <CardContent className="flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
            <div className="overflow-x-auto rounded-[var(--radius-lg)] bg-[var(--color-surface-container-high)] px-4 py-3 text-left font-mono text-sm text-[var(--color-primary)]">
              dotnet add package OpenNist
            </div>
            <div className="flex flex-wrap justify-center gap-3">
              <Button
                asChild
                className="rounded-[var(--radius-lg)] border-0 bg-[var(--color-primary)] text-[var(--on-primary)] hover:opacity-95"
              >
                <Link to="/app/codecs">
                  <AppWindow className="size-4" />
                  Open App
                </Link>
              </Button>
              <Button
                asChild
                variant="outline"
                className="rounded-[var(--radius-lg)] border-[color:var(--effect-ghost-border)] bg-transparent text-[var(--color-primary)] hover:bg-[var(--color-surface-container-low)]"
              >
                <a href="https://github.com/OpenNist/OpenNist" target="_blank" rel="noreferrer">
                  <BookOpenText className="size-4" />
                  Documentation
                </a>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

function RuntimeFlowRow({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-[var(--radius-lg)] bg-[var(--color-surface-container-lowest)] p-4">
      <div className="flex items-center gap-3">
        <div className="flex size-10 items-center justify-center rounded-[var(--radius-md)] bg-[var(--color-surface-container-high)] text-[var(--color-primary)]">
          <Icon className="size-4" />
        </div>
        <div>
          <p className="font-display text-sm font-semibold tracking-[-0.02em] text-[var(--color-primary)]">{label}</p>
          <p className="text-sm text-[var(--color-on-surface-variant)]">{value}</p>
        </div>
      </div>
    </div>
  );
}

function PipelineRow({ source, target }: { source: string; target: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-[var(--radius-lg)] bg-[var(--color-surface-container-lowest)] p-4">
      <span className="text-sm font-medium text-[var(--color-on-surface)]">{source}</span>
      <ArrowRight className="size-4 shrink-0 text-[var(--color-outline)]" />
      <span className="text-right text-sm font-medium text-[var(--color-primary)]">{target}</span>
    </div>
  );
}

function FormatChip({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-[var(--radius-lg)] border border-[color:var(--effect-ghost-border)] bg-[var(--color-surface-container-low)] px-3 py-2 text-center font-mono text-[0.7rem] uppercase tracking-[0.18em] text-[var(--color-primary)]">
      {children}
    </div>
  );
}

function CodeLine({ number, line }: { number: number; line: string }) {
  return (
    <>
      <span className="select-none text-right text-white/28">{number}</span>
      <code>{line}</code>
    </>
  );
}
