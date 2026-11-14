<script lang="ts">
	import Button from "#lib/components/Button/Button.svelte";
	import TextInput from "#lib/components/Inputs/TextInput.svelte";
	import { resolve } from "$app/paths";
    import { superForm } from 'sveltekit-superforms';
    import { untrack } from 'svelte';
	import { yupClient } from "sveltekit-superforms/adapters";
	import { MeasurementEntrySchema, type IMeasurementEntryFormData } from "#lib/forms.ts";

    let { data } = $props();
    const { form, constraints, errors, enhance } = superForm<IMeasurementEntryFormData>(untrack(() => data.form), {
      validators: yupClient(MeasurementEntrySchema),
      validationMethod: 'oninput'
    });
</script>

<section class="center">
    <form class="form-container flex flex-direction-column gap-2" method="POST" use:enhance>
        <a href="{resolve('/')}" data-active="true"><span aria-hidden="true">&lt;</span> Go back</a>

        <h2 class="text-align-center"><span class="text-stroke clr-accent">Baseline</span> / measurement</h2>
        <div class="form-container__inputs flex flex-direction-column gap-1-25">
            <div class="flex gap-1">
                <TextInput
                    {...$constraints.bpSys}
                    bind:value={$form.bpSys}
                    errors={$errors.bpSys}

                    label="BP Sys"
                    type="number"
                    placeholder="Enter bp_sys"
                    name="bp_sys"
                />
                <TextInput
                    {...$constraints.bpDia}
                    bind:value={$form.bpDia}
                    errors={$errors.bpDia}

                    label="BP Dia"
                    type="number"
                    placeholder="Enter bp_dia"
                    name="bp_dia"
                />
            </div>

            <TextInput
                {...$constraints.hrRest}
                bind:value={$form.hrRest}
                errors={$errors.hrRest}

                label="HR Rest"
                type="number"
                placeholder="Enter hr_rest"
                name="hr_rest"
            />

            <TextInput
                {...$constraints.temp}
                bind:value={$form.temp}
                errors={$errors.temp}

                label="Temp"
                type="number"
                placeholder="Enter temp"
                name="temp"
            />

            <TextInput
                {...$constraints.spo2}
                bind:value={$form.spo2}
                errors={$errors.spo2}

                label="Spo2"
                type="number"
                placeholder="Enter spo2"
                name="spo2"
            />
        </div>
        <div class="flex flex-direction-column gap-1">
            <Button type="submit">Submit</Button>
        </div>
    </form>
</section>

<style>
    .form-container {
        min-width: var(--authFormMinWidth);
    }
</style>
