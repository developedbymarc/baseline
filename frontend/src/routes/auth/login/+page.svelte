<script lang="ts">
	import Button from "#lib/components/Button/Button.svelte";
	import TextInput from "#lib/components/Inputs/TextInput.svelte";
	import { resolve } from "$app/paths";
    import { superForm } from 'sveltekit-superforms';
    import { untrack } from 'svelte';
	import { yupClient } from "sveltekit-superforms/adapters";
	import { LoginSchema, type ILoginFormData } from "#lib/forms.ts";

    let { data } = $props();
    const { form, constraints, errors, enhance } = superForm<ILoginFormData>(untrack(() => data.form), {
      validators: yupClient(LoginSchema),
      validationMethod: 'oninput'
    });
</script>

<section class="center">
    <form class="form-container flex flex-direction-column gap-2" method="POST" use:enhance>
        <a href="{resolve('/')}" data-active="true"><span aria-hidden="true">&lt;</span> Go back</a>

        <h2 class="text-align-center"><span class="text-stroke clr-accent">Baseline</span> / login</h2>
        <div class="form-container__inputs flex flex-direction-column gap-1-25">
            <TextInput
                {...$constraints.username}
                bind:value={$form.username}
                errors={$errors.username}

                label="Username"
                type="text"
                placeholder="Enter username"
                name="username"
            />
            <TextInput
                {...$constraints.password}
                bind:value={$form.password}
                errors={$errors.password}

                label="Password"
                type="password"
                placeholder="Enter password"
                name="password"
            />
        </div>
        <div class="flex flex-direction-column gap-1">
            <Button type="submit">Login</Button>
            <p class="text-center">
                Don't have an account?
                <a href="{resolve('auth/signup')}" data-active="true">Sign up</a>
            </p>
        </div>
    </form>
</section>

<style>
    .form-container {
        min-width: var(--authFormMinWidth);
    }
</style>
