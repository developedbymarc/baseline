<script>
	import { userStore } from "#lib/stores.ts";
	import { resolve } from "$app/paths";
	import LinkButton from "../Button/LinkButton.svelte";
	import Logo from "../Logo/Logo.svelte";
	import Themer from "../Themer/Themer.svelte";

</script>

<nav class="main-nav flex gap-2 align-items-center">
    <h1 class="nav__heading">
        <a href="{resolve('/')}"><Logo /></a>
    </h1>

    <div class="flex gap-1-25 align-items-center">
        <!-- <p class="clr-muted">|</p> -->


        {#if $userStore.isLogged}
            <LinkButton href={resolve("/measurement")} class="button-outline button-flat">Measure</LinkButton>
        {:else}
            <LinkButton href={resolve("/auth/login")} class="button-outline button-flat">Log in</LinkButton>
        {/if}

        <p class="clr-muted">|</p>
        <Themer />
    </div>
</nav>

<style>
    .main-nav {
        display: flex;
        justify-content: space-between;
        align-items: center;

        /* background-color: var(--bgOverlay); */
        border-block-end: var(--mainNavBorderThickness) solid var(--bgOverlay);
        padding: var(--mainNavSpacing);
        margin-inline: var(--mainNavMargin);

        .nav__heading { font-size: var(--ffNav); }
    }
</style>
